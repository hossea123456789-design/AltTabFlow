namespace AltTabFlow;

internal sealed class SettingsForm : Form
{
    private readonly Preferences prefs;
    private readonly CheckBox replace=new(){Text="Alt+Tab을 AltTabFlow로 바꾸기"};
    private readonly CheckBox grouped=new(){Text="같은 앱의 창 묶기 · 제목 함께 표시"};
    private readonly CheckBox preview=new(){Text="목록형에서 선택한 창 미리보기"};
    private readonly CheckBox monitor=new(){Text="현재 작업 중인 모니터의 창만 표시"};
    private readonly CheckBox primary=new(){Text="항상 주 모니터에 전환 화면 표시"};
    private readonly CheckBox startup=new(){Text="Windows 시작 시 자동 실행"};
    private readonly CheckBox highlight=new(){Text="선택한 창 모서리에 순환 아우라 표시"};
    private readonly CheckBox emphasis=new(){Text="Alt+Tab 포커스 강조 · 주변 딤드 + 강한 아우라"};
    private readonly CheckBox peek=new(){Text="선택 중인 대상 창을 앞으로 미리 보여주기"};
    private readonly ComboBox mode=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox size=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox theme=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly CheckedListBox apps=new(){CheckOnClick=true,IntegralHeight=false,BorderStyle=BorderStyle.FixedSingle};
    private readonly TextBox excluded=new(){Multiline=true,ScrollBars=ScrollBars.Vertical};
    private readonly TextBox manual=new(){PlaceholderText="프로세스 이름 입력 (예: chrome.exe)"};
    private readonly Panel sample=new(){Size=new Size(560,140)};
    private readonly Label registered=new(){AutoSize=true};
    private readonly List<AppOption> choices=new();
    private sealed record AppOption(string Process,string Name){public override string ToString()=>Name.Equals(Process,StringComparison.OrdinalIgnoreCase)?Name:$"{Name}  ({Process})";}
    public SettingsForm(Preferences prefs,IEnumerable<WindowInfo> windows) {
        this.prefs=prefs;
        Text="AltTabFlow 설정";StartPosition=FormStartPosition.CenterParent;
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;
        ShowInTaskbar=true;TopMost=true;AutoScaleMode=AutoScaleMode.Dpi;
        AutoScaleDimensions=new SizeF(96,96);ClientSize=new Size(650,660);Font=new Font("맑은 고딕",10);
        var heading=new Label{Text="내 방식으로 창 전환",Font=new Font(Font.FontFamily,18,FontStyle.Bold),Location=new Point(24,18),AutoSize=true};Controls.Add(heading);
        var tabs=new TabControl{Location=new Point(24,72),Size=new Size(602,515)};Controls.Add(tabs);
        TabPage Page(string name){var page=new TabPage(name){Padding=new Padding(12),AutoScroll=true};tabs.TabPages.Add(page);return page;}
        var general=Page("기본 동작");var ordering=Page("상단 정렬");var colors=Page("색상 · 효과");var hiding=Page("숨길 앱");
        Label LabelAt(Control host,string text,int x,int y,int width,int height=28){var label=new Label{Text=text,Location=new Point(x,y),Size=new Size(width,height)};host.Controls.Add(label);return label;}
        void Place(Control host,Control control,int x,int y,int w,int h){control.SetBounds(x,y,w,h);host.Controls.Add(control);}
        mode.Items.AddRange(new object[]{"목록","카드","아이콘"});mode.SelectedItem=prefs.View;
        size.Items.AddRange(new object[]{"85%","100%","115%","130%","140%"});size.SelectedItem=prefs.UiScale+"%";if(size.SelectedIndex<0)size.SelectedIndex=1;
        LabelAt(general,"기본 보기",16,22,90);Place(general,mode,110,18,145,30);
        LabelAt(general,"화면 크기",290,22,90);Place(general,size,385,18,160,30);
        replace.Checked=prefs.ReplaceAltTab;grouped.Checked=prefs.GroupApps;preview.Checked=prefs.ShowPreview;
        monitor.Checked=prefs.CurrentMonitorOnly;primary.Checked=prefs.OnPrimaryMonitor;startup.Checked=prefs.AutoStartByDefault;
        var checks=new[]{replace,grouped,preview,primary,monitor,startup};
        for(int i=0;i<checks.Length;i++)Place(general,checks[i],16,72+i*42,548,32);
        LabelAt(general,"Alt를 누른 채 Tab 반복 → 선택 이동 · Alt를 놓으면 전환\n방향키 / 묶음 진입 / 검색 → Enter 또는 Esc까지 화면 유지\n\n자동 실행은 기본으로 켜져 있으며, 해제한 선택은 유지됩니다.",16,340,550,116);
        LabelAt(ordering,"체크한 앱이 먼저 표시됩니다.",16,16,550);
        LabelAt(ordering,"상단 등록 앱: A~Z → 가나다순\n나머지 앱: A~Z → 가나다순 · 등록한 순서와 무관합니다.",16,48,550,52);
        foreach(var w in windows.GroupBy(x=>x.ProcessName,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()))choices.Add(new(w.ProcessName,w.AppName));
        foreach(string process in prefs.Pinned)if(!choices.Any(x=>x.Process.Equals(process,StringComparison.OrdinalIgnoreCase)))choices.Add(new(process,WindowCatalog.AppName(process)));
        Place(ordering,apps,16,110,550,246);apps.AccessibleName="상단 정렬 프로그램 선택";FillChoices(prefs.Pinned);
        Place(ordering,manual,16,372,428,31);
        var add=new Button{Text="등록",FlatStyle=FlatStyle.Flat};Place(ordering,add,454,370,112,33);
        add.Click+=(_,_)=>AddManual();manual.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){AddManual();e.SuppressKeyPress=true;}};
        registered.Location=new Point(16,416);ordering.Controls.Add(registered);UpdateCount();
        apps.ItemCheck+=(_,e)=>{if(IsHandleCreated)BeginInvoke((Action)UpdateCount);};
        LabelAt(ordering,"키보드: Tab으로 목록 이동 · ↑↓ 선택 · Space로 등록/해제",16,448,554,24);
        LabelAt(colors,"색상 프리셋",16,20,150);Place(colors,theme,185,16,376,32);
        theme.Items.AddRange(ThemePalette.All);theme.SelectedItem=ThemePalette.Resolve(prefs);
        Place(colors,sample,16,70,550,150);sample.Paint+=(_,e)=>PaintSample(e.Graphics);
        theme.SelectedIndexChanged+=(_,_)=>{ApplyColors();sample.Invalidate();};
        peek.Checked=prefs.PeekTarget;Place(colors,peek,16,236,550,32);
        highlight.Checked=prefs.HighlightTarget;Place(colors,highlight,16,276,550,32);
        emphasis.Checked=prefs.EmphasizeTarget;Place(colors,emphasis,16,316,550,32);
        LabelAt(colors,"프리셋의 강조색으로 빛의 꼬리가 빠르게 순환합니다.\n포커스 강조를 켜면 주변은 어둡게, 대상 창은 선명하게 표시합니다.\n최소화된 창은 실제로 전환할 때 복원합니다.\n저장하면 적용됩니다.",16,370,550,96);
        LabelAt(hiding,"숨길 프로그램 · 한 줄에 하나씩 입력",16,20,550);
        LabelAt(hiding,"예: EXCEL, chrome, slack (.exe는 생략 가능)\n다시 보이게 하려면 이름을 지우고 저장하세요.",16,54,550,58);
        excluded.Text=string.Join(Environment.NewLine,prefs.Excluded);Place(hiding,excluded,16,130,550,220);
        LabelAt(hiding,"전환 목록에서 우클릭해 숨길 수도 있습니다.\n프로그램은 종료되지 않으며 AltTabFlow 목록에서만 제외됩니다.",16,374,550,70);
        var save=new Button{Text="저장",Location=new Point(506,605),Size=new Size(120,36),FlatStyle=FlatStyle.Flat};
        var cancel=new Button{Text="취소",DialogResult=DialogResult.Cancel,Location=new Point(385,605),Size=new Size(106,36),FlatStyle=FlatStyle.Flat};
        save.Click+=(_,_)=>Save();Controls.Add(save);Controls.Add(cancel);AcceptButton=save;CancelButton=cancel;ApplyColors();
    }
    private ThemePalette Palette=>(ThemePalette?)theme.SelectedItem??ThemePalette.Resolve(prefs);
    private void ApplyColors(){
        var p=Palette;BackColor=p.Bg;ForeColor=p.Ink;
        void Style(Control host){foreach(Control c in host.Controls){c.ForeColor=p.Ink;c.BackColor=c is TextBox or ListBox or ComboBox or Button?p.Raised:p.Bg;Style(c);}}
        Style(this);sample.BackColor=p.Surface;sample.Invalidate();
    }
    private void PaintSample(Graphics g){
        var p=Palette;g.Clear(p.Surface);float s=DeviceDpi/96f;
        using var accent=new SolidBrush(p.Accent);using var selected=new SolidBrush(p.Selected);
        g.FillRectangle(selected,12*s,54*s,526*s,58*s);g.FillRectangle(accent,12*s,54*s,4*s,58*s);
        TextRenderer.DrawText(g,"AltTabFlow · "+p.Name,Font,new Point((int)(18*s),(int)(15*s)),p.Ink);
        TextRenderer.DrawText(g,"Chrome  ·  전환 대상",Font,new Point((int)(28*s),(int)(67*s)),p.Accent);
        TextRenderer.DrawText(g,"앱 이름과 창 제목을 함께 표시합니다",Font,new Point((int)(28*s),(int)(91*s)),p.Muted);
    }
    private void FillChoices(IEnumerable<string> selected){
        var set=selected.ToHashSet(StringComparer.OrdinalIgnoreCase);apps.BeginUpdate();apps.Items.Clear();
        foreach(var option in choices.OrderBy(x=>x.Name,AppNameComparer.Instance))apps.Items.Add(option,set.Contains(option.Process));apps.EndUpdate();
    }
    private void AddManual(){
        var names=Preferences.NormalizeApps(manual.Text.Split(new[]{',','\r','\n'},StringSplitOptions.RemoveEmptyEntries));
        var selected=apps.CheckedItems.Cast<AppOption>().Select(x=>x.Process).Concat(names).ToList();
        foreach(string process in names)if(!choices.Any(x=>x.Process.Equals(process,StringComparison.OrdinalIgnoreCase)))choices.Add(new(process,WindowCatalog.AppName(process)));
        FillChoices(selected);manual.Clear();UpdateCount();
    }
    private void UpdateCount()=>registered.Text=$"상단 등록 {apps.CheckedItems.Count}개 · 실행 중인 앱과 기존 등록 앱을 표시합니다.";
    protected override void OnShown(EventArgs e){base.OnShown(e);if(Owner!=null){Icon=Owner.Icon;var work=Screen.FromControl(Owner).WorkingArea;Location=new Point(Math.Clamp(Owner.Left+(Owner.Width-Width)/2,work.Left,Math.Max(work.Left,work.Right-Width)),Math.Clamp(Owner.Top+(Owner.Height-Height)/2,work.Top,Math.Max(work.Top,work.Bottom-Height)));}}
    private void Save(){
        try{
            Preferences.StartupEnabled=startup.Checked;prefs.StartWithWindows=startup.Checked;
            prefs.View=mode.SelectedItem?.ToString()??"목록";prefs.UiScale=int.Parse((size.SelectedItem?.ToString()??"100%").TrimEnd('%'));
            prefs.ReplaceAltTab=replace.Checked;prefs.GroupApps=grouped.Checked;prefs.ShowPreview=preview.Checked;
            prefs.CurrentMonitorOnly=monitor.Checked;prefs.OnPrimaryMonitor=primary.Checked;
            prefs.ColorPreset=Palette.Id;prefs.LightTheme=Palette.Light;prefs.HighlightTarget=highlight.Checked;
            prefs.EmphasizeTarget=emphasis.Checked;prefs.PeekTarget=peek.Checked;
            prefs.Excluded=Preferences.NormalizeApps(excluded.Text.Split(new[]{'\r','\n',','},StringSplitOptions.RemoveEmptyEntries));
            prefs.Pinned=apps.CheckedItems.Cast<AppOption>().OrderBy(x=>x.Name,AppNameComparer.Instance).Select(x=>x.Process).ToList();
            DialogResult=DialogResult.OK;Close();
        }catch(Exception ex){MessageBox.Show(this,"설정을 저장하지 못했습니다.\n"+ex.Message,"AltTabFlow",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
}
