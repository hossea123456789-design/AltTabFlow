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
        var detail=new Label{Text="공개 GitHub 릴리스 확인 → SHA-256 검사 → 자동 교체 → 재시작\n진행 상황은 이 창에 계속 표시됩니다. 이 창은 ESC를 누르지 않아도 닫을 수 있습니다.",
            Location=new Point(22,54),Size=new Size(590,43)};
        var tokenLabel=new Label{Text="공개 실행 파일만 확인합니다. GitHub 로그인이나 인증 토큰은 필요 없습니다.",Location=new Point(22,168),Size=new Size(590,36)};
        var info=new Label{Text="인증키 없이 업데이트합니다. 진행 중 '취소'는 다운로드를 중단합니다.",
            Location=new Point(22,225),Size=new Size(590,36)};
        Controls.AddRange(new Control[]{title,detail,status,tokenLabel,info,CheckUpdate,cancel});
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
        status.Text="공개 GitHub 배포에서 버전을 확인합니다. 인증 없이 ‘업데이트 확인’을 누르세요.";
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
                operation.Token);
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
