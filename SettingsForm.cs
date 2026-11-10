namespace AltTabFlow;

internal sealed class SettingsForm : Form
{
    private readonly Preferences prefs;
    private readonly FlowCheck replace=new(){Text="Alt+Tab 전환 사용",Description="Windows 전환 화면을 AltTabFlow로 바꿉니다."};
    private readonly FlowCheck grouped=new(){Text="같은 앱의 창 묶기",Description="앱별로 모으고 대표 창의 제목을 함께 표시합니다."};
    private readonly FlowCheck preview=new(){Text="선택한 창 미리보기",Description="목록 오른쪽에 미리보기를 표시합니다."};
    private readonly FlowCheck monitor=new(){Text="현재 모니터의 창만 표시",Description="다른 모니터에 열린 창은 목록에서 제외합니다."};
    private readonly FlowCheck primary=new(){Text="항상 주 모니터에 표시",Description="어느 화면에서 호출하든 같은 위치에 나타납니다."};
    private readonly FlowCheck startup=new(){Text="Windows 시작 시 자동 실행",Description="로그인하면 백그라운드에서 실행합니다."};
    private readonly FlowCheck highlight=new(){Text="창 테두리 아우라",Description="프리셋의 강조색이 선택한 창 모서리를 빠르게 돕니다."};
    private readonly FlowCheck emphasis=new(){Text="Alt+Tab 포커스 강조",Description="주변을 어둡게 하고 대상 창의 아우라를 강화합니다."};
    private readonly FlowCheck peek=new(){Text="선택한 창을 앞으로 미리 표시",Description="창의 실제 내용을 확인하며 전환할 수 있습니다."};
    private readonly FlowSelect mode=new(),size=new();
    private readonly FlowAppList apps=new();
    private readonly TextBox excluded=new(){Multiline=true,AcceptsReturn=true,AcceptsTab=false,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None};
    private readonly TextBox manual=new(){PlaceholderText="프로세스 이름 · 예: chrome.exe",BorderStyle=BorderStyle.None};
    private readonly Panel sample=new();
    private readonly Label registered=new(){AutoSize=false};
    private readonly List<AppOption> choices=new();
    private readonly List<Panel> pages=new();
    private readonly List<FlowButton> navigation=new(),swatches=new();
    private readonly List<Font> ownedFonts=new();
    private readonly FlowButton save=new(){Text="변경 사항 저장",Primary=true},cancel=new(){Text="취소",Quiet=true,DialogResult=DialogResult.Cancel};
    private readonly Label footer=new(){Text="변경 사항은 저장 후 적용됩니다.",AutoSize=false};
    private ThemePalette palette;
    private int currentPage;
    private readonly CancellationTokenSource iconLoading=new();
    private bool iconsStarted,iconsDisposed;
    internal bool PreviewRendering {get;set;}
    internal bool PreviewOnly {get;set;}
    protected override bool ShowWithoutActivation=>PreviewRendering||base.ShowWithoutActivation;
    // Reference identity stays stable when a background icon arrives. A mutable
    // record hash would lose checked entries from FlowAppList's HashSet.
    private sealed class AppOption(string process,string name,WindowInfo? window=null):IAppListItem{
        public string Process=>process;public string Name=>name;public WindowInfo? Window=>window;
        public string AppName=>Name;public string ProcessName=>Process;
        public Image? AppIcon{get;set;}=window==null?null:WindowCatalog.CachedAppIcon(window);
        public override string ToString()=>Name.Equals(Process,StringComparison.OrdinalIgnoreCase)?Name:$"{Name}  ·  {Process}";
    }
    private Font MakeFont(float size,bool bold=false){var f=new Font("맑은 고딕",size,bold?FontStyle.Bold:FontStyle.Regular);ownedFonts.Add(f);return f;}
    public SettingsForm(Preferences prefs,IEnumerable<WindowInfo> windows){
        this.prefs=prefs;palette=ThemePalette.Resolve(prefs);
        Text="AltTabFlow 설정";StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;
        ShowInTaskbar=true;TopMost=true;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);ClientSize=new Size(860,792);Font=MakeFont(10);
        SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);
        using(var stream=typeof(SettingsForm).Assembly.GetManifestResourceStream("AltTabFlow.Assets.AltTabFlow.ico"))Icon=new Icon(stream!);
        LabelAt(this,"AltTabFlow",24,18,180,28,16,true);LabelAt(this,"설정",216,16,500,36,21,true);LabelAt(this,"창 전환을 내 작업 방식에 맞춰 보세요.",216,59,596,24,10,false,true);
        string[] names={"기본 동작","상단 정렬","색상 · 효과","숨길 앱"};
        for(int i=0;i<names.Length;i++){int index=i;var nav=new FlowButton{Text=names[i],AlignLeft=true,Quiet=true,AccessibleName=names[i]};Place(this,nav,24,104+i*58,166,48);nav.Click+=(_,_)=>SelectPage(index);navigation.Add(nav);var page=new Panel{AutoScroll=true,TabStop=false,Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right};Place(this,page,216,100,620,606);pages.Add(page);}
        LabelAt(this,"Tab 이동\nSpace 선택\nEsc 취소",40,370,150,74,9,false,true);
        var general=pages[0];PageHeading(general,"기본 동작","표시 방식과 전환 화면의 동작을 정합니다.");
        var display=Card(general,0,74,598,98);LabelAt(display,"기본 보기",18,12,250,23,9,false,true);LabelAt(display,"화면 크기",310,12,250,23,9,false,true);
        mode.Items.AddRange(new object[]{"목록","카드","아이콘"});mode.SelectedItem=prefs.View;Place(display,mode,16,42,272,40);mode.AccessibleName="기본 보기";
        size.Items.AddRange(new object[]{"85%","100%","115%","130%","140%"});size.SelectedItem=prefs.UiScale+"%";if(!size.Items.Contains(prefs.UiScale+"%"))size.SelectedItem="100%";Place(display,size,308,42,272,40);size.AccessibleName="화면 크기";
        replace.Checked=prefs.ReplaceAltTab;grouped.Checked=prefs.GroupApps;preview.Checked=prefs.ShowPreview;monitor.Checked=prefs.CurrentMonitorOnly;primary.Checked=prefs.OnPrimaryMonitor;startup.Checked=prefs.AutoStartByDefault;
        var behaviors=Card(general,0,184,598,360);var checks=new[]{replace,grouped,preview,primary,monitor,startup};for(int i=0;i<checks.Length;i++)Place(behaviors,checks[i],8,6+i*58,582,58);
        LabelAt(general,"Alt+Tab → 방향키로 이동 → 키를 모두 놓으면 전환\nCtrl+Alt+Space로 열면 키를 놓아도 선택 화면을 유지합니다.",4,558,590,46,9,false,true);
        var ordering=pages[1];PageHeading(ordering,"상단 정렬","등록한 앱을 먼저, 각 그룹 안에서는 A~Z → 가나다순으로 표시합니다.");
        foreach(var w in windows.GroupBy(x=>x.ProcessName,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()))choices.Add(new(w.ProcessName,w.AppName,w));
        foreach(string process in prefs.Pinned)if(!choices.Any(x=>x.Process.Equals(process,StringComparison.OrdinalIgnoreCase)))choices.Add(new(process,WindowCatalog.AppName(process)));
        var listCard=Card(ordering,0,74,598,342);Place(listCard,apps,10,12,578,318);FillChoices(prefs.Pinned);
        var entry=Card(ordering,0,430,598,78);Place(entry,manual,16,27,438,26);manual.AccessibleName="상단 정렬에 등록할 프로세스 이름";
        var add=new FlowButton{Text="앱 등록"};Place(entry,add,468,18,112,40);add.Click+=(_,_)=>AddManual();manual.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){AddManual();e.SuppressKeyPress=true;}};
        Place(ordering,registered,4,524,594,24);registered.Tag="muted";UpdateCount();apps.ChecksChanged+=(_,_)=>UpdateCount();
        LabelAt(ordering,"↑↓로 앱 선택 · Space로 등록/해제\n실행하지 않은 앱도 프로세스 이름으로 등록할 수 있습니다.",4,562,590,44,9,false,true);
        var colors=pages[2];PageHeading(colors,"색상 · 효과","마음에 드는 색과 선택한 창을 강조하는 방식을 고르세요.");
        for(int i=0;i<ThemePalette.All.Length;i++){var preset=ThemePalette.All[i];var button=new FlowButton{Text=preset.Name,Swatch=preset,AccessibleName=preset.Name+" 색상 프리셋",Font=MakeFont(9)};Place(colors,button,(i%4)*151,74+(i/4)*62,145,54);button.Click+=(_,_)=>{palette=preset;ApplyColors();};swatches.Add(button);}
        Place(colors,sample,0,210,598,114);sample.Paint+=(_,e)=>PaintSample(e.Graphics);
        var effects=Card(colors,0,340,598,192);peek.Checked=prefs.PeekTarget;highlight.Checked=prefs.HighlightTarget;emphasis.Checked=prefs.EmphasizeTarget;
        var fxChecks=new[]{peek,highlight,emphasis};for(int i=0;i<fxChecks.Length;i++)Place(effects,fxChecks[i],8,6+i*60,582,60);
        LabelAt(colors,"선택한 프리셋은 전환 화면과 아우라에 함께 적용됩니다.\n최소화된 창은 실제로 전환할 때 복원합니다.",4,550,590,46,9,false,true);
        var hiding=pages[3];PageHeading(hiding,"숨길 앱","목록에서 제외할 프로그램을 한 줄에 하나씩 입력하세요.");
        var hideCard=Card(hiding,0,74,598,330);LabelAt(hideCard,"프로세스 이름",18,16,550,24,10,true);excluded.Text=string.Join(Environment.NewLine,prefs.Excluded);excluded.PlaceholderText="예: chrome\nEXCEL\nslack";excluded.AccessibleName="숨길 프로그램 · 한 줄에 하나";Place(hideCard,excluded,18,58,562,252);
        var info=Card(hiding,0,420,598,122);LabelAt(info,"프로그램은 종료되지 않습니다.",18,18,558,26,11,true);LabelAt(info,"AltTabFlow 목록에서만 숨깁니다. .exe는 생략해도 됩니다.\n다시 표시하려면 이름을 지우고 저장하세요.",18,57,558,48,10,false,true);
        LabelAt(hiding,"전환 목록의 앱을 우클릭해서 숨길 수도 있습니다.",4,562,590,28,9,false,true);
        Place(this,footer,24,736,440,28);footer.Tag="muted";footer.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
        Place(this,cancel,588,730,96,42);cancel.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;Place(this,save,696,730,140,42);save.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;save.Click+=(_,_)=>Save();AcceptButton=save;CancelButton=cancel;
        ApplyColors();SelectPage(0);
    }
    private static void Place(Control host,Control c,int x,int y,int w,int h){c.SetBounds(x,y,w,h);c.TabIndex=host.Controls.Count;host.Controls.Add(c);}
    private Label LabelAt(Control host,string text,int x,int y,int w,int h,float font=10,bool bold=false,bool muted=false){var label=new Label{Text=text,Font=MakeFont(font,bold),Tag=muted?"muted":null,AutoSize=false};Place(host,label,x,y,w,h);return label;}
    private void PageHeading(Control host,string title,string subtitle){LabelAt(host,title,0,0,598,32,16,true);LabelAt(host,subtitle,0,38,598,26,9.5f,false,true);}
    private FlowCard Card(Control host,int x,int y,int w,int h){var c=new FlowCard();Place(host,c,x,y,w,h);return c;}
    private void SelectPage(int index){currentPage=index;for(int i=0;i<pages.Count;i++){pages[i].Visible=i==index;navigation[i].Selected=i==index;navigation[i].Invalidate();}pages[index].BringToFront();}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData is (Keys.Control|Keys.Tab) or (Keys.Control|Keys.Shift|Keys.Tab)){SelectPage((currentPage+((keyData&Keys.Shift)!=0?3:1))%4);navigation[currentPage].Focus();return true;}return base.ProcessCmdKey(ref msg,keyData);}
    private void ApplyColors(){var p=palette;BackColor=p.Bg;ForeColor=p.Ink;void Style(Control host){foreach(Control c in host.Controls){c.ForeColor=Equals(c.Tag,"muted")?p.Muted:p.Ink;c.BackColor=host is FlowCard?p.Surface:p.Bg;if(c is FlowCard card){card.Palette=p;c.BackColor=p.Surface;}if(c is FlowButton button)button.Palette=p;if(c is FlowCheck check)check.Palette=p;if(c is FlowAppList list){list.Palette=p;list.BackColor=p.Surface;list.ItemHeight=(int)(48*DeviceDpi/96f);}Style(c);c.Invalidate();}}Style(this);foreach(var button in swatches)button.Selected=button.Swatch==p;sample.Invalidate();UiPaint.TitleBar(this,p);Invalidate();}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);UiPaint.TitleBar(this,palette);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);float s=DeviceDpi/96f;using var pen=new Pen(UiPaint.Border(palette));e.Graphics.DrawLine(pen,24*s,ClientSize.Height-76*s,ClientSize.Width-24*s,ClientSize.Height-76*s);}
    private void PaintSample(Graphics g){var p=palette;int D(float n)=>(int)Math.Round(n*DeviceDpi/96f);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.Clear(p.Bg);UiPaint.Box(g,new Rectangle(1,1,sample.Width-3,sample.Height-3),p.Surface,D(12),UiPaint.Border(p));UiPaint.Text(g,"미리보기  ·  "+p.Name,Font,new Rectangle(D(18),D(12),sample.Width-D(36),D(24)),p.Muted);var r=new Rectangle(D(14),D(46),sample.Width-D(28),D(54));UiPaint.Box(g,r,p.Selected,D(9),p.Accent);AppIconDrawing.Paint(g,new Rectangle(D(26),D(59),D(28),D(28)),choices.FirstOrDefault(x=>x.Process.Equals("chrome",StringComparison.OrdinalIgnoreCase))?.AppIcon,"Chrome",Font,p);UiPaint.Text(g,"Chrome",Font,new Rectangle(D(64),D(51),D(150),D(24)),p.Ink);UiPaint.Text(g,"작업 중인 창 제목 · 선택한 창으로 전환",Font,new Rectangle(D(64),D(76),sample.Width-D(144),D(22)),p.Muted);UiPaint.Check(g,new Rectangle(sample.Width-D(48),D(63),D(20),D(20)),p.Accent);}
    private void FillChoices(IEnumerable<string> selected){var set=selected.ToHashSet(StringComparer.OrdinalIgnoreCase);apps.BeginUpdate();apps.Items.Clear();apps.ResetChecks();foreach(var option in choices.OrderBy(x=>x.Name,AppNameComparer.Instance)){apps.Items.Add(option);apps.SetChecked(option,set.Contains(option.Process));}apps.EndUpdate();}
    private void AddManual(){var names=Preferences.NormalizeApps(manual.Text.Split(new[]{',','\r','\n'},StringSplitOptions.RemoveEmptyEntries));var selected=apps.CheckedItems.Cast<AppOption>().Select(x=>x.Process).Concat(names).ToList();var added=new List<AppOption>();foreach(string process in names)if(!choices.Any(x=>x.Process.Equals(process,StringComparison.OrdinalIgnoreCase))){var item=new AppOption(process,WindowCatalog.AppName(process));choices.Add(item);added.Add(item);}FillChoices(selected);manual.Clear();UpdateCount();if(!PreviewRendering)_=LoadIcons(added.ToArray());}
    private void UpdateCount()=>registered.Text=$"상단 등록 {apps.CheckedItems.Count()}개 · 등록 순서와 무관하게 이름순으로 정렬합니다.";
    protected override void OnShown(EventArgs e){base.OnShown(e);if(PreviewRendering)return;if(!iconsStarted){iconsStarted=true;_=LoadIcons(choices.ToArray());}var work=Owner==null?Screen.FromControl(this).WorkingArea:Screen.FromControl(Owner).WorkingArea;Height=Math.Min(Height,work.Height-24);Location=new Point(Math.Clamp((Owner?.Left??work.Left)+((Owner?.Width??work.Width)-Width)/2,work.Left,Math.Max(work.Left,work.Right-Width)),Math.Clamp(work.Top+(work.Height-Height)/2,work.Top,Math.Max(work.Top,work.Bottom-Height)));}
    private async Task LoadIcons(AppOption[] items){
        var token=iconLoading.Token;
        foreach(var item in items){if(token.IsCancellationRequested||IsDisposed)return;if(item.AppIcon!=null)continue;
            Image? icon=null;try{icon=await Task.Run(()=>{
                if(token.IsCancellationRequested)return null;
                var window=item.Window;
                if(window==null){string? path=RegisteredAppPath(item.Process);if(path==null)return null;window=new WindowInfo(0,item.Name,item.Process,item.Name,path,0,false);}
                return WindowCatalog.GetIcon(window);
            },token);}catch(OperationCanceledException){return;}catch{ }
            if(token.IsCancellationRequested||IsDisposed)return;if(icon!=null){item.AppIcon=icon;apps.Invalidate();sample.Invalidate();}
        }
    }
    internal static string? RegisteredAppPath(string process){
        foreach(var hive in new[]{Microsoft.Win32.Registry.CurrentUser,Microsoft.Win32.Registry.LocalMachine}){
            try{using var key=hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+process+".exe");if(key?.GetValue("") is string value){string path=Environment.ExpandEnvironmentVariables(value).Trim('"');if(!path.StartsWith(@"\\")&&File.Exists(path))return path;}}catch{ }
        }
        return null;
    }
    private void Save(){try{if(!PreviewOnly)Preferences.StartupEnabled=startup.Checked;ApplyValues();DialogResult=DialogResult.OK;Close();}catch(Exception ex){MessageBox.Show(this,"설정을 저장하지 못했습니다.\n"+ex.Message,"AltTabFlow",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
    private void ApplyValues(){prefs.StartWithWindows=startup.Checked;prefs.View=mode.SelectedItem?.ToString()??"목록";prefs.UiScale=int.Parse((size.SelectedItem?.ToString()??"100%").TrimEnd('%'));prefs.ReplaceAltTab=replace.Checked;prefs.GroupApps=grouped.Checked;prefs.ShowPreview=preview.Checked;prefs.CurrentMonitorOnly=monitor.Checked;prefs.OnPrimaryMonitor=primary.Checked;prefs.ColorPreset=palette.Id;prefs.LightTheme=palette.Light;prefs.HighlightTarget=highlight.Checked;prefs.EmphasizeTarget=emphasis.Checked;prefs.PeekTarget=peek.Checked;prefs.Excluded=Preferences.NormalizeApps(excluded.Text.Split(new[]{'\r','\n',','},StringSplitOptions.RemoveEmptyEntries));prefs.Pinned=apps.CheckedItems.Cast<AppOption>().OrderBy(x=>x.Name,AppNameComparer.Instance).Select(x=>x.Process).ToList();}
    internal void TestPage(int page,ThemePalette? p=null){if(p!=null){palette=p;ApplyColors();}SelectPage(page);}
    internal void TestApplyValues()=>ApplyValues();
    internal Task TestLoadIcons()=>LoadIcons(choices.ToArray());
    internal Panel TestCurrentPage=>pages[currentPage];
    protected override void Dispose(bool disposing){if(disposing&&!iconsDisposed){iconsDisposed=true;iconLoading.Cancel();iconLoading.Dispose();}base.Dispose(disposing);if(disposing){foreach(var font in ownedFonts)font.Dispose();ownedFonts.Clear();}}
}

