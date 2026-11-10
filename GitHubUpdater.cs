using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AltTabFlow;

// This private repository cannot be queried anonymously. The only secret is a
// fine-grained GitHub PAT with Contents:read and Actions:read for AltTabFlow.
// It lives in Windows Credential Manager, NEVER in settings.json or logs.
internal static class GitHubUpdater
{
    private const string Repo="hossea123456789-design/AltTabFlow";
    internal const string ArtifactName="AltTabFlow-win-x64-CANDIDATE";
    private static readonly Uri ApiRoot=new("https://api.github.com/");
    private static bool busy;
    private sealed record Candidate(Version Version,long RunId,long ArtifactId,string? Digest,string Sha);

    public static void ShowWindow(Action exit,IWin32Window? owner=null)
    {
        var existing=Application.OpenForms.OfType<UpdateWindow>().FirstOrDefault();
        if(existing!=null){existing.Activate();return;}
        using var dialog=new UpdateWindow(exit);
        if(owner is Control control&&control.Visible&&!control.IsDisposed)dialog.ShowDialog(owner);
        else dialog.ShowDialog();
    }

    public static async Task CheckAndInstallAsync(Form owner,Action exit,Action<string>? report=null,string? suppliedToken=null,CancellationToken cancellation=default)
    {
        if(busy)return;
        busy=true;
        try {
            if(owner.IsDisposed)return;
            cancellation.ThrowIfCancellationRequested();
            string token=GitHubCredential.Read()??"";
            bool newlyEntered=string.IsNullOrWhiteSpace(token);
            if(newlyEntered){
                token=suppliedToken?.Trim()??"";
                if(string.IsNullOrWhiteSpace(token))
                    throw new InvalidOperationException("GitHub 읽기 전용 토큰을 입력한 뒤 확인을 눌러주세요.");
            }
            using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(7)};
            report?.Invoke("GitHub의 최신 빌드 확인 중...");
            Candidate update=await GetCandidate(http,token,cancellation);
            if(owner.IsDisposed)return;
            if(newlyEntered)GitHubCredential.Write(token); // store only after successful GitHub API authentication
            Version current=typeof(Program).Assembly.GetName().Version??new Version(0,0);
            if(update.Version<=current) {
                report?.Invoke("현재 최신 버전입니다.");
                MessageBox.Show(owner,$"현재 버전 {current.ToString(3)}\n최신 성공 빌드 {update.Version.ToString(3)}\n\n새 버전이 없습니다.","AltTabFlow 업데이트",MessageBoxButtons.OK,MessageBoxIcon.Information);
                return;
            }
            if(MessageBox.Show(owner,$"현재 {current.ToString(3)} → 최신 {update.Version.ToString(3)}\n\n최신 main 브랜치의 성공한 Windows 실행 후보를 설치합니다. ZIP 압축 해제나 수동 파일 교체는 필요 없습니다.\n\n이 빌드는 Windows 자동 검사 후보이며, 관리자 게임·P4 실사용 검증 완료를 보증하지 않습니다.\n\n다운로드하고 업데이트하시겠습니까?","AltTabFlow 업데이트",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) {
                report?.Invoke("업데이트를 취소했습니다.");
                return;
            }
            report?.Invoke("업데이트 파일 다운로드 및 검증 중...");
            var stage=await DownloadAndStage(http,token,update,cancellation);
            cancellation.ThrowIfCancellationRequested();
            if(owner.IsDisposed)return;
            report?.Invoke("새 실행 파일 검증 완료. 프로그램을 다시 시작합니다.");
            UpdateInstaller.Launch(stage,Environment.ProcessPath!,Environment.ProcessId);
            exit(); // clean shutdown restores any temporarily changed Z-order
        }
        catch(OperationCanceledException){
            report?.Invoke("업데이트 확인 또는 다운로드를 취소했습니다.");
        }
        catch(HttpRequestException ex) when(ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound) {
            // Token may have expired or lack permissions. The next click asks again.
            GitHubCredential.Delete();
            report?.Invoke("GitHub 인증 권한을 확인해야 합니다.");
            if(!owner.IsDisposed)MessageBox.Show(owner,"비공개 GitHub 저장소 인증에 실패했습니다.\n\n다시 '업데이트 확인'을 누르고 저장소 Contents(읽기) 및 Actions(읽기) 권한이 있는 토큰을 입력해 주세요.\n\n"+ex.Message,"업데이트 인증",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
        catch(Exception ex) {
            report?.Invoke("업데이트 실패 — 현재 버전 유지");
            if(!owner.IsDisposed)MessageBox.Show(owner,"업데이트를 완료하지 못했습니다. 기존 실행본은 유지됩니다.\n"+ex.Message,"AltTabFlow 업데이트",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
        finally {busy=false;}
    }

    private static async Task<JsonDocument> Json(HttpClient http,string token,string apiPath,CancellationToken cancellation)
    {
        var response=await Send(http,token,new Uri(ApiRoot,apiPath),cancellation);
        using(response) {
            response.EnsureSuccessStatusCode();
            var stream=await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream,cancellationToken:cancellation);
        }
    }

    private static async Task<HttpResponseMessage> Send(HttpClient http,string token,Uri url,CancellationToken cancellation)
    {
        // Redirects to Actions artifact storage must never receive GitHub credentials.
        for(int redirects=0;redirects<6;redirects++) {
            if(!ApprovedUrl(url))throw new InvalidOperationException("허용되지 않은 업데이트 다운로드 주소입니다.");
            using var message=new HttpRequestMessage(HttpMethod.Get,url);
            message.Headers.UserAgent.ParseAdd("AltTabFlow-Updater/1.1");
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            message.Headers.Add("X-GitHub-Api-Version","2022-11-28");
            if(url.Host=="api.github.com")message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
            var response=await http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,cancellation);
            if(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect) {
                var location=response.Headers.Location;response.Dispose();
                if(location==null)throw new InvalidOperationException("업데이트 다운로드 경로가 비어 있습니다.");
                url=location.IsAbsoluteUri?location:new Uri(url,location);
                continue;
            }
            return response;
        }
        throw new InvalidOperationException("업데이트 다운로드 경로 변경이 너무 많습니다.");
    }

    internal static bool ApprovedUrl(Uri uri)
    {
        if(uri.Scheme!="https"||uri.UserInfo.Length!=0)return false;
        var host=uri.Host.ToLowerInvariant();
        return host=="api.github.com"||host=="github.com"||
               host.EndsWith(".githubusercontent.com",StringComparison.Ordinal)||
               host.EndsWith(".blob.core.windows.net",StringComparison.Ordinal);
    }

    internal static Version ParseVersion(string xml)
    {
        var x=XDocument.Parse(xml);
        string value=x.Descendants().First(e=>e.Name.LocalName=="Version").Value.Trim();
        if(!Version.TryParse(value,out var version))throw new InvalidDataException("프로젝트 버전 정보를 확인하지 못했습니다.");
        return version;
    }

    private static async Task<Candidate> GetCandidate(HttpClient http,string token,CancellationToken cancellation)
    {
        using var main=await Json(http,token,$"repos/{Repo}/commits/main",cancellation);
        string head=main.RootElement.GetProperty("sha").GetString()!;
        using var runs=await Json(http,token,$"repos/{Repo}/actions/workflows/windows-build.yml/runs?branch=main&status=success&per_page=10",cancellation);
        var run=runs.RootElement.GetProperty("workflow_runs").EnumerateArray()
            .FirstOrDefault(e=>e.GetProperty("head_sha").GetString()==head&&e.GetProperty("conclusion").GetString()=="success");
        if(run.ValueKind==JsonValueKind.Undefined)throw new InvalidOperationException("최신 main 커밋의 Windows 빌드가 아직 성공하지 않았습니다. 프로그램은 현재 버전을 유지합니다.");
        long runId=run.GetProperty("id").GetInt64();
        using var project=await Json(http,token,$"repos/{Repo}/contents/AltTabFlow.csproj?ref={Uri.EscapeDataString(head)}",cancellation);
        var base64=project.RootElement.GetProperty("content").GetString()!.Replace("\n","").Replace("\r","");
        var version=ParseVersion(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
        using var artifacts=await Json(http,token,$"repos/{Repo}/actions/runs/{runId}/artifacts?per_page=100",cancellation);
        var artifact=artifacts.RootElement.GetProperty("artifacts").EnumerateArray()
            .FirstOrDefault(e=>e.GetProperty("name").GetString()==ArtifactName&&!e.GetProperty("expired").GetBoolean());
        if(artifact.ValueKind==JsonValueKind.Undefined)throw new InvalidOperationException("다운로드 가능한 최신 Windows 실행 후보가 아직 없습니다.");
        string? digest=artifact.TryGetProperty("digest",out var dg)?dg.GetString():null;
        return new(version,runId,artifact.GetProperty("id").GetInt64(),digest,head);
    }

    private static async Task<string> DownloadAndStage(HttpClient http,string token,Candidate candidate,CancellationToken cancellation)
    {
        string baseDir=Path.Combine(Preferences.DataDirectory,"updates",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        string zip=Path.Combine(baseDir,"candidate.zip");
        try {
            using(var response=await Send(http,token,new Uri(ApiRoot,$"repos/{Repo}/actions/artifacts/{candidate.ArtifactId}/zip"),cancellation)) {
                response.EnsureSuccessStatusCode();
                if(response.Content.Headers.ContentLength is >200_000_000)throw new InvalidDataException("업데이트 ZIP 크기가 허용 범위를 초과했습니다.");
                await using var read=await response.Content.ReadAsStreamAsync();
                await using var write=File.Create(zip);
                var buffer=new byte[81920];long size=0;
                while(true){int n=await read.ReadAsync(buffer,cancellation);if(n==0)break;size+=n;if(size>200_000_000)throw new InvalidDataException("업데이트 ZIP이 너무 큽니다.");await write.WriteAsync(buffer.AsMemory(0,n),cancellation);}
            }
            if(candidate.Digest?.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)==true) {
                await using var input=File.OpenRead(zip);
                string hash=Convert.ToHexString(await SHA256.HashDataAsync(input,cancellation));
                if(!hash.Equals(candidate.Digest[7..],StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("GitHub 아티팩트 SHA-256 확인에 실패했습니다.");
            }
            cancellation.ThrowIfCancellationRequested();
            using(var archive=ZipFile.OpenRead(zip))UpdateInstaller.ExtractCandidate(archive,baseDir);
            string exe=Path.Combine(baseDir,"AltTabFlow.exe");
            var fileVersion=FileVersionInfo.GetVersionInfo(exe).ProductVersion??"";
            var match=Regex.Match(fileVersion,@"^\d+\.\d+\.\d+(?:\.\d+)?");
            if(!match.Success||!Version.TryParse(match.Value,out var actual)||actual!=candidate.Version)
                throw new InvalidDataException($"다운로드 파일의 버전이 GitHub 빌드 버전과 다릅니다 ({fileVersion}).");
            return baseDir;
        } catch {try{Directory.Delete(baseDir,true);}catch{}throw;}
    }
}

internal static class GitHubCredential
{
    private const string Target="AltTabFlow/GitHub-Private-Repo-ReadOnly";
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct Credential {
        public int Flags,Type;
        public string TargetName,Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist,AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias,UserName;
    }
    [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern bool CredWrite(ref Credential credential,int flags);
    [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern bool CredRead(string target,int type,int flags,out IntPtr credential);
    [DllImport("advapi32.dll",EntryPoint="CredDeleteW",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern bool CredDelete(string target,int type,int flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr credential);

    public static string? Read()
    {
        if(!CredRead(Target,1,0,out var ptr))return null;
        try {
            var cred=Marshal.PtrToStructure<Credential>(ptr);
            if(cred.CredentialBlobSize<=0||cred.CredentialBlobSize>5120)return null;
            var bytes=new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob,bytes,0,bytes.Length);
            return Encoding.Unicode.GetString(bytes);
        } finally {CredFree(ptr);}
    }
    public static void Write(string token)
    {
        byte[] bytes=Encoding.Unicode.GetBytes(token);
        if(bytes.Length>5120)throw new InvalidOperationException("인증 토큰이 너무 깁니다.");
        IntPtr data=Marshal.AllocHGlobal(bytes.Length);
        try {
            Marshal.Copy(bytes,0,data,bytes.Length);
            var cred=new Credential {
                Type=1,TargetName=Target,UserName=Environment.UserName,
                CredentialBlob=data,CredentialBlobSize=bytes.Length,Persist=2,
                Comment="Private AltTabFlow GitHub Actions/Contents read-only token",
                TargetAlias=""
            };
            if(!CredWrite(ref cred,0))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        } finally {Marshal.FreeHGlobal(data);}
    }
    public static void Delete()=>CredDelete(Target,1,0);
}

// Runs from a temporary COPY of the old EXE and only after old process exit.
// Atomic File.Replace keeps a rollback copy and preserves settings in AppData.
internal static class UpdateInstaller
{
    internal static void ExtractCandidate(ZipArchive archive,string destination)
    {
        var exe=archive.GetEntry("artifacts/ci-publish/AltTabFlow.exe")??archive.GetEntry("AltTabFlow.exe");
        if(exe==null||exe.Length<100_000)throw new InvalidDataException("Windows 실행 파일이 ZIP에 없습니다.");
        static void Extract(ZipArchiveEntry entry,string file){
            using var input=entry.Open();using var output=File.Create(file);input.CopyTo(output);
        }
        Extract(exe,Path.Combine(destination,"AltTabFlow.exe"));
        foreach(string name in new[]{"Game-compat-mode.cmd","Run-GameCompat.ps1","사용법.txt"}) {
            var file=archive.GetEntry(name);
            if(file!=null&&file.Length<500_000)Extract(file,Path.Combine(destination,name));
        }
    }
    internal static bool NeedsElevation(string folder)
    {
        try {
            string test=Path.Combine(folder,".AltTabFlow-write-"+Guid.NewGuid().ToString("N"));
            using(File.Create(test)){}
            File.Delete(test);return false;
        } catch(UnauthorizedAccessException){return true;}
          catch(IOException){return true;}
    }
    public static void Launch(string stage,string target,int parentPid)
    {
        string updater=Path.Combine(stage,"AltTabFlow-Updater.exe");
        File.Copy(Environment.ProcessPath!,updater,true);
        var psi=new ProcessStartInfo(updater){UseShellExecute=true,WorkingDirectory=stage};
        if(NeedsElevation(Path.GetDirectoryName(target)!))psi.Verb="runas";
        psi.ArgumentList.Add("--apply-update");
        psi.ArgumentList.Add(stage);psi.ArgumentList.Add(target);
        psi.ArgumentList.Add(parentPid.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(stage,"AltTabFlow.exe")))));
        if(Process.Start(psi)==null)throw new InvalidOperationException("업데이트 도우미를 시작하지 못했습니다.");
    }
    public static void Run(string[] args)
    {
        if(args.Length!=5)throw new InvalidDataException("업데이트 인수 오류");
        string stage=Path.GetFullPath(args[1]),target=Path.GetFullPath(args[2]);
        if(!string.Equals(Path.GetFileName(target),"AltTabFlow.exe",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("업데이트 대상이 AltTabFlow.exe가 아닙니다.");
        if(!stage.StartsWith(Path.GetFullPath(Path.Combine(Preferences.DataDirectory,"updates"))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("업데이트 임시 경로가 올바르지 않습니다.");
        if(!int.TryParse(args[3],out int parentPid)||parentPid<=0||!Regex.IsMatch(args[4],@"^[A-Fa-f0-9]{64}$"))
            throw new InvalidDataException("업데이트 확인 값이 올바르지 않습니다.");
        string staged=Path.Combine(stage,"AltTabFlow.exe");
        try {
            try {using var previous=Process.GetProcessById(parentPid);if(!previous.WaitForExit(30000))throw new TimeoutException("기존 AltTabFlow가 종료되지 않았습니다.");}
            catch(ArgumentException){} // already exited
            string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(staged)));
            if(!hash.Equals(args[4],StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("교체할 EXE의 SHA-256 검증에 실패했습니다.");
            string backup=target+".previous";
            string incoming=target+".incoming";
            File.Copy(staged,incoming,true);
            bool replaced=false;
            for(int attempt=0;attempt<12&&!replaced;attempt++){
                try {File.Replace(incoming,target,backup,true);replaced=true;}
                catch(IOException) when(attempt<11){Thread.Sleep(500);}
            }
            if(!replaced)throw new IOException("실행 파일을 교체하지 못했습니다.");
            foreach(var name in new[]{"Game-compat-mode.cmd","Run-GameCompat.ps1","사용법.txt"}) {
                var source=Path.Combine(stage,name);
                if(File.Exists(source))File.Copy(source,Path.Combine(Path.GetDirectoryName(target)!,name),true);
            }
            var next=new ProcessStartInfo(target){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(target)!};
            next.ArgumentList.Add("--background");
            if(Process.Start(next)==null)throw new IOException("새 버전의 실행을 시작하지 못했습니다.");
            try {File.Delete(Path.Combine(stage,"candidate.zip"));}catch{}
        } catch(Exception ex){
            MessageBox.Show("자동 업데이트에 실패했습니다. 기존 실행 파일의 이전 버전 백업(.previous)을 확인하세요.\n"+ex.Message,"AltTabFlow 업데이트",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
}

internal static class UpdaterTests
{
    public static void Run()
    {
        var checks=new List<object>();int failures=0;
        void Test(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)failures++;}
        Test("Version reads from csproj",GitHubUpdater.ParseVersion("<Project><PropertyGroup><Version>1.1.9</Version></PropertyGroup></Project>")==new Version(1,1,9));
        Test("new version exceeds installed",new Version(1,1,9)>new Version(1,1,8));
        Test("equal version is not an update",!(new Version(1,1,8)>new Version(1,1,8)));
        Test("github API URL accepted",GitHubUpdater.ApprovedUrl(new Uri("https://api.github.com/repos/hossea123456789-design/AltTabFlow")));
        Test("githubusercontent download URL accepted",GitHubUpdater.ApprovedUrl(new Uri("https://results-receiver.actions.githubusercontent.com/path")));
        Test("redirect with userinfo rejected",!GitHubUpdater.ApprovedUrl(new Uri("https://user:secret@api.github.com/")));
        Test("insecure URL rejected",!GitHubUpdater.ApprovedUrl(new Uri("http://api.github.com/")));
        Test("untrusted redirect host rejected",!GitHubUpdater.ApprovedUrl(new Uri("https://evil.example.com/")));
        string temp=Path.Combine(Path.GetTempPath(),"altflow-update-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            using var memory=new MemoryStream();
            using(var writer=new ZipArchive(memory,ZipArchiveMode.Create,true)) {
                var exe=writer.CreateEntry("artifacts/ci-publish/AltTabFlow.exe");
                using(var s=exe.Open())s.Write(new byte[100_001]);
                var script=writer.CreateEntry("Run-GameCompat.ps1");
                using(var s=new StreamWriter(script.Open()))s.Write("echo test");
                var extra=writer.CreateEntry("../../bad.txt");
                using(var s=new StreamWriter(extra.Open()))s.Write("should not be extracted");
            }
            memory.Position=0;
            using(var z=new ZipArchive(memory,ZipArchiveMode.Read,true))UpdateInstaller.ExtractCandidate(z,temp);
            Test("extracts exe from CI candidate layout",new FileInfo(Path.Combine(temp,"AltTabFlow.exe")).Length==100_001);
            Test("extracts game compatibility script beside exe",File.Exists(Path.Combine(temp,"Run-GameCompat.ps1")));
            Test("ignores unsafe ZIP entries",!File.Exists(Path.Combine(temp,"bad.txt")));
        } finally {Directory.Delete(temp,true);}
        string report=Path.Combine(Preferences.DataDirectory,"updater-test.json");
        Directory.CreateDirectory(Preferences.DataDirectory);
        File.WriteAllText(report,JsonSerializer.Serialize(new{tests=checks,failures},new JsonSerializerOptions{WriteIndented=true}));
        Environment.ExitCode=failures>0?1:0;
    }
}
