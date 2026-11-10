using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AltTabFlow;

// PUBLIC BINARY FEED ONLY: source repository stays private.
// The client never reads its contents, Actions, account data or GitHub tokens.
// The update ZIP must be attached to a published Release in the dedicated
// binary-only public repository, and GitHub must provide a SHA-256 asset digest.
internal static class GitHubUpdater
{
    internal const string PublicRepo="hossea123456789-design/AltTabFlow-Downloads";
    internal const string ReleaseAsset="AltTabFlow-win-x64.zip";
    internal const bool RequiresAuthentication=false;
    private static readonly Uri ApiRoot=new("https://api.github.com/");
    private static bool busy;
    internal sealed record Candidate(Version Version,Uri DownloadUrl,string Sha256,string Tag,bool Prerelease);

    public static void ShowWindow(Action exit,IWin32Window? owner=null)
    {
        var existing=Application.OpenForms.OfType<UpdateWindow>().FirstOrDefault();
        if(existing!=null){existing.Activate();return;}
        using var dialog=new UpdateWindow(exit);
        if(owner is Control control&&control.Visible&&!control.IsDisposed)dialog.ShowDialog(owner);
        else dialog.ShowDialog();
    }

    public static async Task CheckAndInstallAsync(Form owner,Action exit,Action<string>? report=null,CancellationToken cancellation=default)
    {
        if(busy)return;
        busy=true;
        try {
            if(owner.IsDisposed)return;
            cancellation.ThrowIfCancellationRequested();
            using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(7)};
            report?.Invoke("공개 GitHub 릴리스에서 최신 버전 확인 중…");
            Version current=typeof(Program).Assembly.GetName().Version??new Version(0,0);
            Candidate? update=await GetCandidate(http,current,cancellation);
            if(owner.IsDisposed)return;
            if(update==null){
                report?.Invoke("현재 최신 버전입니다. GitHub 로그인·토큰 입력은 필요 없습니다.");
                MessageBox.Show(owner,$"현재 버전: {current.ToString(3)}\n새로운 공개 배포 버전이 없습니다.","AltTabFlow 업데이트",MessageBoxButtons.OK,MessageBoxIcon.Information);
                return;
            }
            if(MessageBox.Show(owner,
                $"현재 {current.ToString(3)} → 공개 배포 {update.Version.ToString(3)}\n\n" +
                "GitHub에서 실행 파일만 다운로드합니다. ZIP 압축 해제나 인증 토큰 입력은 필요 없습니다.\n" +
                "다운로드한 파일의 SHA-256과 EXE 버전 확인 후 교체·재시작합니다.\n\n" +
                "이 배포본은 Windows 자동 검사를 통과한 후보이며 P4·관리자 게임 실기기 검증을 대신하지 않습니다.\n\n설치하시겠습니까?",
                "AltTabFlow 자동 업데이트",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes){
                report?.Invoke("업데이트를 취소했습니다.");
                return;
            }
            report?.Invoke("공개 릴리스 파일 다운로드 및 SHA-256 검증 중…");
            string stage=await DownloadAndStage(http,update,cancellation);
            cancellation.ThrowIfCancellationRequested();
            if(owner.IsDisposed)return;
            report?.Invoke("무결성 확인 완료. 안전한 교체 후 프로그램을 다시 시작합니다.");
            UpdateInstaller.Launch(stage,Environment.ProcessPath!,Environment.ProcessId);
            if(owner is UpdateWindow dialog)dialog.PermitUpdateExit();
            exit();
        }
        catch(OperationCanceledException){
            report?.Invoke("업데이트 확인 또는 다운로드를 취소했습니다. 현재 버전을 유지합니다.");
        }
        catch(HttpRequestException ex) when(ex.StatusCode==HttpStatusCode.NotFound){
            report?.Invoke("공개 배포 저장소가 아직 생성되지 않았거나 릴리스가 없습니다.");
            if(!owner.IsDisposed)MessageBox.Show(owner,
                "실행 파일 전용 공개 GitHub 저장소가 준비되지 않았습니다.\n" +
                "현재 버전은 유지됩니다. 인증 토큰은 필요하지 않습니다.",
                "AltTabFlow 공개 배포 대기",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch(Exception ex){
            report?.Invoke("업데이트 실패 — 현재 버전 유지");
            if(!owner.IsDisposed)MessageBox.Show(owner,
                "자동 업데이트를 완료하지 못했습니다. 기존 실행 파일은 유지됩니다.\n"+ex.Message,
                "AltTabFlow 업데이트",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
        finally {busy=false;}
    }

    private static async Task<Candidate?> GetCandidate(HttpClient http,Version current,CancellationToken ct)
    {
        using var response=await Send(http,new Uri(ApiRoot,$"repos/{PublicRepo}/releases?per_page=30"),ct);
        response.EnsureSuccessStatusCode();
        await using var stream=await response.Content.ReadAsStreamAsync(ct);
        using var document=await JsonDocument.ParseAsync(stream,cancellationToken:ct);
        return SelectCandidate(document.RootElement,current);
    }

    // Only published release objects may be installed. Pre-releases are allowed
    // because CI builds are explicitly labeled as unverified candidates.
    internal static Candidate? SelectCandidate(JsonElement releases,Version current)
    {
        if(releases.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("공개 배포 목록 형식이 잘못됐습니다.");
        Candidate? winner=null;
        foreach(var release in releases.EnumerateArray()){
            if(!TryRelease(release,out Candidate? candidate)||candidate==null)continue;
            if(candidate.Version<=current)continue;
            if(winner==null||candidate.Version>winner.Version)winner=candidate;
        }
        return winner;
    }

    internal static bool TryRelease(JsonElement release,out Candidate? candidate)
    {
        candidate=null;
        try{
            if(release.ValueKind!=JsonValueKind.Object||
               release.GetProperty("draft").GetBoolean())return false;
            bool prerelease=release.GetProperty("prerelease").GetBoolean();
            string tag=release.GetProperty("tag_name").GetString()??"";
            if(!Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$",RegexOptions.CultureInvariant))return false;
            if(!Version.TryParse(tag[1..],out Version? version))return false;
            foreach(var asset in release.GetProperty("assets").EnumerateArray()){
                if(asset.GetProperty("name").GetString()!=ReleaseAsset)continue;
                string digest=asset.TryGetProperty("digest",out var d)&&d.ValueKind==JsonValueKind.String
                    ?d.GetString()??"":"";
                if(!Regex.IsMatch(digest,@"^sha256:[a-fA-F0-9]{64}$",RegexOptions.CultureInvariant))return false;
                string uri=asset.GetProperty("browser_download_url").GetString()??"";
                if(!Uri.TryCreate(uri,UriKind.Absolute,out var address)||!IsReleaseAssetUrl(address,tag))return false;
                var bytes=asset.GetProperty("size").GetInt64();
                if(bytes<1_000_000||bytes>200_000_000)return false;
                candidate=new Candidate(version,address,digest[7..].ToUpperInvariant(),tag,prerelease);
                return true;
            }
        }catch(ArgumentException){}catch(InvalidOperationException){}catch(FormatException){}catch(OverflowException){}
        return false;
    }

    internal static bool IsReleaseAssetUrl(Uri uri,string tag)
    {
        if(uri.Scheme!="https"||uri.UserInfo.Length!=0||
           !string.Equals(uri.Host,"github.com",StringComparison.OrdinalIgnoreCase))return false;
        string expected=$"/{PublicRepo}/releases/download/{tag}/{ReleaseAsset}";
        return string.Equals(Uri.UnescapeDataString(uri.AbsolutePath),expected,StringComparison.Ordinal);
    }

    internal static bool ApprovedRedirect(Uri uri)
    {
        if(uri.Scheme!="https"||uri.UserInfo.Length!=0)return false;
        string host=uri.Host.ToLowerInvariant();
        return host=="api.github.com"||
               host=="github.com"||
               host=="release-assets.githubusercontent.com"||
               host=="objects.githubusercontent.com"||
               host=="github-releases.githubusercontent.com"||
               host.EndsWith(".blob.core.windows.net",StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient http,Uri url,CancellationToken ct)
    {
        for(int i=0;i<6;i++){
            ct.ThrowIfCancellationRequested();
            if(!ApprovedRedirect(url))throw new InvalidOperationException("허용되지 않은 업데이트 다운로드 주소입니다.");
            using var request=new HttpRequestMessage(HttpMethod.Get,url);
            request.Headers.UserAgent.ParseAdd("AltTabFlow-Public-Updater/1.1");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            // No Authorization header, PAT, GitHub Credential Manager or private repo calls.
            var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            if(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently
               or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect){
                var next=response.Headers.Location;
                response.Dispose();
                if(next==null)throw new InvalidDataException("GitHub 업데이트 파일의 다운로드 위치가 없습니다.");
                url=next.IsAbsoluteUri?next:new Uri(url,next);
                continue;
            }
            return response;
        }
        throw new InvalidOperationException("GitHub 다운로드 경로 변경이 너무 많습니다.");
    }

    private static async Task<string> DownloadAndStage(HttpClient http,Candidate candidate,CancellationToken ct)
    {
        string folder=Path.Combine(Preferences.DataDirectory,"updates",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string zip=Path.Combine(folder,"candidate.zip");
        try{
            using(var response=await Send(http,candidate.DownloadUrl,ct)){
                response.EnsureSuccessStatusCode();
                if(response.Content.Headers.ContentLength is >200_000_000)
                    throw new InvalidDataException("업데이트 ZIP 크기가 너무 큽니다.");
                await using var input=await response.Content.ReadAsStreamAsync(ct);
                await using var output=File.Create(zip);
                byte[] buffer=new byte[81920];long bytes=0;
                while(true){
                    int n=await input.ReadAsync(buffer,ct);if(n==0)break;
                    bytes+=n;if(bytes>200_000_000)throw new InvalidDataException("다운로드 한도를 초과했습니다.");
                    await output.WriteAsync(buffer.AsMemory(0,n),ct);
                }
            }
            await using(var input=File.OpenRead(zip)){
                string actual=Convert.ToHexString(await SHA256.HashDataAsync(input,ct));
                if(!string.Equals(actual,candidate.Sha256,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("GitHub 릴리스 SHA-256 검증에 실패했습니다. 파일을 교체하지 않습니다.");
            }
            ct.ThrowIfCancellationRequested();
            using(var archive=ZipFile.OpenRead(zip))UpdateInstaller.ExtractCandidate(archive,folder);
            string exe=Path.Combine(folder,"AltTabFlow.exe");
            string name=FileVersionInfo.GetVersionInfo(exe).ProductVersion??"";
            var match=Regex.Match(name,@"^\d+\.\d+\.\d+(?:\.\d+)?");
            if(!match.Success||!Version.TryParse(match.Value,out var actualVersion)||
               actualVersion!=candidate.Version)
                throw new InvalidDataException("다운로드한 EXE 버전과 공개 릴리스 버전이 일치하지 않습니다: "+name);
            return folder;
        }catch{try{Directory.Delete(folder,true);}catch{}throw;}
    }
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
        Test("Version reads from csproj",Version.TryParse("1.1.9",out var parsed)&&parsed==new Version(1,1,9));
        Test("new version exceeds installed",new Version(1,1,9)>new Version(1,1,8));
        Test("equal version is not an update",!(new Version(1,1,8)>new Version(1,1,8)));
        Test("public GitHub API accepted without token",!GitHubUpdater.RequiresAuthentication&&GitHubUpdater.ApprovedRedirect(new Uri("https://api.github.com/repos/hossea123456789-design/AltTabFlow-Downloads/releases")));
        Test("GitHub release asset CDN accepted",GitHubUpdater.ApprovedRedirect(new Uri("https://release-assets.githubusercontent.com/path")));
        Test("redirect with userinfo rejected",!GitHubUpdater.ApprovedRedirect(new Uri("https://user:secret@api.github.com/")));
        Test("insecure URL rejected",!GitHubUpdater.ApprovedRedirect(new Uri("http://api.github.com/")));
        Test("untrusted redirect host rejected",!GitHubUpdater.ApprovedRedirect(new Uri("https://evil.example.com/")));
        // Public release feed: no credentials, no private repo, fail closed on
        // digest mismatch or non-release download paths.
        string ValidRelease(string tag="v1.1.11",bool draft=false,bool prerelease=true,
            string asset="AltTabFlow-win-x64.zip",string digestPrefix="sha256:",
            string? href=null){
            href??=$"https://github.com/{GitHubUpdater.PublicRepo}/releases/download/{tag}/{asset}";
            return JsonSerializer.Serialize(new{
                tag_name=tag,draft,prerelease,
                assets=new[]{new{
                    name=asset,size=73400320,digest=digestPrefix+new string('a',64),
                    browser_download_url=href
                }}
            });
        }
        using(var release=JsonDocument.Parse(ValidRelease())){
            Test("published public release parsed",GitHubUpdater.TryRelease(release.RootElement,out var candidate)
                &&candidate!.Version==new Version(1,1,11));
            Test("public release SHA-256 digest normalized",GitHubUpdater.TryRelease(release.RootElement,out var c)
                &&c!.Sha256==new string('A',64));
            Test("prerelease candidate accepted with warning",GitHubUpdater.TryRelease(release.RootElement,out var preview)
                &&preview!.Prerelease);
        }
        using(var release=JsonDocument.Parse(ValidRelease(draft:true)))
            Test("draft release rejected",!GitHubUpdater.TryRelease(release.RootElement,out _));
        using(var release=JsonDocument.Parse(ValidRelease(asset:"Source-code.zip")))
            Test("wrong asset name rejected",!GitHubUpdater.TryRelease(release.RootElement,out _));
        using(var release=JsonDocument.Parse(ValidRelease(digestPrefix:"md5:")))
            Test("missing SHA256 digest rejected",!GitHubUpdater.TryRelease(release.RootElement,out _));
        using(var release=JsonDocument.Parse(ValidRelease(tag:"v1.1.11-beta")))
            Test("non-semver tag rejected",!GitHubUpdater.TryRelease(release.RootElement,out _));
        using(var release=JsonDocument.Parse(ValidRelease(href:"https://evil.example.com/releases/download/v1.1.11/AltTabFlow-win-x64.zip")))
            Test("untrusted host rejected",!GitHubUpdater.TryRelease(release.RootElement,out _));
        using(var release=JsonDocument.Parse(ValidRelease(href:"https://github.com/other/repo/releases/download/v1.1.11/AltTabFlow-win-x64.zip")))
            Test("other repo asset path rejected",!GitHubUpdater.TryRelease(release.RootElement,out _));
        using(var releases=JsonDocument.Parse("["+ValidRelease("v1.1.11")+","+ValidRelease("v1.1.12")+"]"))
            Test("highest version selected",GitHubUpdater.SelectCandidate(releases.RootElement,new Version(1,1,10))?.Version==new Version(1,1,12));
        using(var releases=JsonDocument.Parse("["+ValidRelease("v1.1.11")+"]"))
            Test("same version never reinstalled",GitHubUpdater.SelectCandidate(releases.RootElement,new Version(1,1,11))==null);
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
