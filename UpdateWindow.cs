namespace AltTabFlow;

// Explicit, visible, focusable update window. Replaces the temporary token
// popup that could end up behind the TopMost Alt+Tab switcher/settings dialog.
internal sealed class UpdateWindow : Form
{
    private readonly Action exit;
    private readonly Label status=new(){
        AutoSize=false,Text="최신 성공 빌드를 확인할 수 있습니다.",
        Location=new Point(22,98),Size=new Size(590,68)
    };
    internal readonly TextBox TokenEntry=new(){
        UseSystemPasswordChar=true,PlaceholderText="GitHub 읽기 전용 Fine-grained PAT (최초 1회)",
        Location=new Point(22,190),Width=590,AccessibleName="GitHub 저장소 읽기 전용 토큰"
    };
    internal readonly Button CheckUpdate=new(){
        Text="업데이트 확인",Location=new Point(346,270),Size=new Size(130,38)
    };
    private readonly Button cancel=new(){
        Text="닫기",Location=new Point(490,270),Size=new Size(122,38)
    };
    private CancellationTokenSource? operation;
    private bool working,allowUpdateExit;
    internal void PermitUpdateExit()=>allowUpdateExit=true;

    internal UpdateWindow(Action exit)
    {
        this.exit=exit;
        Text="AltTabFlow · 업데이트";
        ClientSize=new Size(636,330);
        Font=new Font("맑은 고딕",10);
        StartPosition=FormStartPosition.CenterScreen;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=MinimizeBox=false;MinimizeBox=false;
        TopMost=true;ShowInTaskbar=true;
        using(var resource=typeof(UpdateWindow).Assembly.GetManifestResourceStream("AltTabFlow.Assets.AltTabFlow.ico"))
            if(resource!=null)Icon=new Icon(resource);
        var title=new Label{Text="업데이트 확인 및 자동 설치",Font=new Font(Font,FontStyle.Bold),Location=new Point(22,18),Size=new Size(590,33)};
        var detail=new Label{Text="최신 GitHub 빌드 확인 → 무결성 검사 → 자동 교체 → 재시작\n진행 상황은 이 창에 계속 표시됩니다. 이 창은 ESC를 누르지 않아도 닫을 수 있습니다.",
            Location=new Point(22,54),Size=new Size(590,43)};
        var tokenLabel=new Label{Text="비공개 저장소 인증 · 처음 한 번만 읽기 전용 토큰을 입력합니다.",Location=new Point(22,168),Size=new Size(590,22)};
        var info=new Label{Text="기존 설정은 유지합니다. 작업 진행 중 '취소'를 누르면 다운로드를 중단합니다.",
            Location=new Point(22,225),Size=new Size(590,36)};
        Controls.AddRange(new Control[]{title,detail,status,tokenLabel,TokenEntry,info,CheckUpdate,cancel});
        CheckUpdate.Click+=async (_,_)=>await CheckAsync();
        cancel.Click+=(_,_)=>{
            if(operation!=null){
                status.Text="진행 중인 요청을 취소하고 있습니다...";
                operation.Cancel();
            }else Close();
        };
        AcceptButton=CheckUpdate;
        CancelButton=cancel;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Keyboard focus is placed in this dialog, never on the Alt+Tab overlay.
        CheckUpdate.Focus();
        if(GitHubCredential.Read()!=null)status.Text="저장된 GitHub 인증을 사용합니다. ‘업데이트 확인’을 누르세요.";
        else status.Text="아래 칸에 읽기 전용 GitHub 토큰을 입력하고 ‘업데이트 확인’을 누르세요.";
    }

    private async Task CheckAsync()
    {
        if(working)return;
        working=true;
        CheckUpdate.Enabled=false;
        operation=new CancellationTokenSource();
        try {
            await GitHubUpdater.CheckAndInstallAsync(this,exit,
                s=>{if(!IsDisposed)status.Text=s;},
                TokenEntry.Text,operation.Token);
        } finally {
            working=false;
            operation?.Dispose();operation=null;
            if(!IsDisposed){
                CheckUpdate.Enabled=true;
                cancel.Text="닫기";
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if(working&&!allowUpdateExit){
            e.Cancel=true;
            operation?.Cancel();
            status.Text="취소 중입니다...";
        }
        base.OnFormClosing(e);
    }
}
