using System.Text.Json;
using System.Drawing.Imaging;
namespace AltTabFlow;

internal static class UiTests
{
    internal static IEnumerable<Control> Walk(Control c){foreach(Control child in c.Controls){yield return child;foreach(var inner in Walk(child))yield return inner;}}
    public static void Run(){
        var tests=new List<object>();int failures=0;void Check(string name,bool ok){tests.Add(new{name,passed=ok});if(!ok)failures++;}
        string output=Path.Combine(Preferences.DataDirectory,"ui-previews");Directory.CreateDirectory(output);
        var windows=new List<WindowInfo>{new(101,"검토 중인 문서","chrome","Chrome","chrome.exe",0,false),new(102,"개발 작업","Code","Visual Studio Code","Code.exe",1,false),new(103,"일정 및 메시지","slack","Slack","slack.exe",2,false)};
        int dpi=96;
        // Drive native TextBox message preprocessing, including the form's real
        // AcceptButton. No SendInput, user settings or startup registration.
        var editPrefs=new Preferences{StartWithWindows=false,Excluded=new(){"original_app"}};
        using(var editForm=new SettingsForm(editPrefs,windows){PreviewRendering=true,PreviewOnly=true,TopMost=false,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){
            editForm.Show();editForm.TestPage(3);var editor=Walk(editForm).OfType<TextBox>().Single(t=>t.Multiline);
            editor.Text="chrome.exe";editor.SelectionStart=editor.TextLength;editor.Focus();
            var enter=Message.Create(editor.Handle,0x100,(nint)13,0);bool consumed=editor.PreProcessMessage(ref enter);
            if(!consumed&&!editor.IsDisposed)Native.SendMessageTimeout(editor.Handle,0x102,(nint)13,0,2,200,out _);
            Check("hidden-app Enter inserts native newline",!editor.IsDisposed&&editor.Text=="chrome.exe\r\n");
            Check("hidden-app Enter keeps dialog open without saving",editForm.Visible&&editForm.DialogResult==DialogResult.None&&editPrefs.Excluded.SequenceEqual(new[]{"original_app"}));
            if(!editor.IsDisposed){editor.AppendText("EXCEL.exe");Walk(editForm).OfType<FlowButton>().Single(b=>b.Primary).PerformClick();}
            Check("explicit save applies multiple hidden app lines",editPrefs.Excluded.SequenceEqual(new[]{"chrome","EXCEL"}));
        }
        foreach(var p in ThemePalette.All){
            var prefs=new Preferences{ColorPreset=p.Id,LightTheme=p.Light,StartWithWindows=false,GroupApps=false,ShowPreview=true,UiScale=115,Pinned=new(){"chrome","missing_app"},Excluded=new(){"hidden_app"}};
            string original=JsonSerializer.Serialize(prefs);
            using var form=new SettingsForm(prefs,windows){PreviewRendering=true,TopMost=false,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)};
            form.Show();dpi=form.DeviceDpi;
            for(int page=0;page<4;page++){
                form.TestPage(page,p);form.PerformLayout();Application.DoEvents();
                using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,$"settings-{p.Id}-{page}.png"),ImageFormat.Png);
                var host=form.TestCurrentPage;
                Check($"{p.Id} page {page}: cards and controls fit content width",Walk(host).All(c=>c.Left>=0&&c.Right<=c.Parent!.ClientSize.Width+1));
            }
            Check(p.Id+" preview does not mutate settings",JsonSerializer.Serialize(prefs)==original);
            form.TestApplyValues();Check(p.Id+" save preserves preferences and unlaunched pinned app",prefs.ColorPreset==p.Id&&prefs.StartWithWindows==false&&!prefs.GroupApps&&prefs.ShowPreview&&prefs.UiScale==115&&prefs.Pinned.Contains("missing_app")&&prefs.Pinned.Contains("chrome")&&prefs.Excluded.SequenceEqual(new[]{"hidden_app"}));
            form.Close();
        }
        using(var form=new SettingsForm(new Preferences{ColorPreset="rose",StartWithWindows=false},windows){PreviewRendering=true,TopMost=false,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){
            form.Show();form.Height=(int)(600*form.DeviceDpi/96f);form.TestPage(0);Application.DoEvents();
            var save=Walk(form).OfType<FlowButton>().Single(x=>x.Primary);
            Check("short work area keeps save and cancel visible",save.Bottom<=form.ClientSize.Height&&save.Top>form.TestCurrentPage.Bottom);
            Check("short work area scrolls settings body",form.TestCurrentPage.VerticalScroll.Visible);
            form.TestPage(2,ThemePalette.All[6]);form.TestApplyValues();
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"settings-short-light.png"),ImageFormat.Png);form.Close();
        }
        using(var selector=new FlowSelect()){
            selector.Items.AddRange(new object[]{"목록","카드","아이콘"});selector.SelectedIndex=0;selector.TestKey(Keys.Right);Check("view selector arrow selects next value",Equals(selector.SelectedItem,"카드"));selector.TestKey(Keys.Left);Check("view selector arrow selects previous value",Equals(selector.SelectedItem,"목록"));selector.TestKey(Keys.Left);Check("selector boundary stays valid",selector.SelectedIndex==0);
        }
        var iconPrefs=new Preferences{StartWithWindows=false,Pinned=new(){"chrome"}};
        using(var form=new SettingsForm(iconPrefs,windows){PreviewRendering=true,PreviewOnly=true,TopMost=false,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)})using(var icon=new Bitmap(28,28)){
            using(var g=Graphics.FromImage(icon))g.Clear(Color.Coral);
            form.Show();form.TestPage(1);var list=Walk(form).OfType<FlowAppList>().Single();var item=list.Items.Cast<IAppListItem>().Single(x=>x.ProcessName=="chrome");item.AppIcon=icon;
            Check("loading icon preserves checked app identity",list.CheckedItems.Contains(item));
            using(var bitmap=new Bitmap(list.Width,list.Height)){list.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));int D(float v)=>(int)Math.Round(v*list.DeviceDpi/96f);Check("app selection row draws actual supplied icon",bitmap.GetPixel(D(56),D(24)).ToArgb()==Color.Coral.ToArgb());}
            form.TestApplyValues();Check("saving after icon load preserves pinned app",iconPrefs.Pinned.SequenceEqual(new[]{"chrome"}));
            item.AppIcon=null;using(var bitmap=new Bitmap(40,40)){using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Magenta);AppIconDrawing.Paint(g,new Rectangle(2,2,28,28),null,"Chrome",form.Font,ThemePalette.All[0]);}Check("missing app icon draws identifying fallback badge",bitmap.GetPixel(5,5).ToArgb()!=Color.Magenta.ToArgb());}form.Close();
        }
        var ownIconWindow=new WindowInfo(0,"Icon fixture","icon_fixture","Icon fixture",Environment.ProcessPath!,0,false);
        using(var form=new SettingsForm(new Preferences{StartWithWindows=false},new[]{ownIconWindow}){PreviewRendering=true,PreviewOnly=true,TopMost=false,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){
            form.Show();var job=form.TestLoadIcons();var deadline=Environment.TickCount64+3000;while(!job.IsCompleted&&Environment.TickCount64<deadline){Application.DoEvents();Thread.Sleep(1);}
            var list=Walk(form).OfType<FlowAppList>().Single();Check("background icon extraction fills settings item",job.IsCompletedSuccessfully&&((IAppListItem)list.Items[0]).AppIcon!=null);form.Close();
        }
        using(var form=new SettingsForm(new Preferences{StartWithWindows=false},new[]{ownIconWindow with{ProcessName="icon_cancel",Path=Environment.ProcessPath!+".missing"}}){PreviewRendering=true,PreviewOnly=true,TopMost=false,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){
            form.Show();var job=form.TestLoadIcons();form.Close();var deadline=Environment.TickCount64+3000;while(!job.IsCompleted&&Environment.TickCount64<deadline){Application.DoEvents();Thread.Sleep(1);}Check("closing settings safely cancels pending icon load",form.IsDisposed&&job.IsCompletedSuccessfully);
        }
        using(var form=new Form{StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),ShowInTaskbar=false})using(var selector=new FlowSelect{Palette=ThemePalette.All[3],Width=272,Height=40}){
            form.Controls.Add(selector);form.CreateControl();selector.CreateControl();selector.Items.AddRange(new object[]{"목록","카드","아이콘"});selector.SelectedIndex=0;
            var menu=selector.TestMenu();Check("selector opens a themed native menu",menu.Visible&&menu.Items.Count==3&&menu.BackColor==ThemePalette.All[3].Surface);
            using(var bitmap=new Bitmap(menu.Width,menu.Height)){menu.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"selector-rose.png"),ImageFormat.Png);}
            menu.Items[2].PerformClick();menu.Close();Check("menu selection updates selected view",Equals(selector.SelectedItem,"아이콘"));bool reusable=true;for(int i=0;i<10;i++){var next=selector.TestMenu();next.Close(ToolStripDropDownCloseReason.Keyboard);reusable&=ReferenceEquals(next,menu)&&!next.IsDisposed;}Check("selector menu survives repeated close and reopen",reusable);
        }
        using(var list=new FlowAppList()){
            list.Items.AddRange(new object[]{"Chrome","Slack"});list.SelectedIndex=0;list.TestKey(Keys.Space);Check("Space registers selected app",list.CheckedItems.SequenceEqual(new object[]{"Chrome"}));Check("app registration exposed to accessibility",(list.AccessibilityObject.GetChild(0)!.State&AccessibleStates.Checked)!=0);list.TestKey(Keys.Space);Check("Space unregisters selected app",!list.CheckedItems.Any());
        }
        // Wheel must move the selection without pinning the plain Alt+Tab session.
        // Check release via the same SwitcherForm.NavigationReleased path used by
        // real KeyboardHook, with a fake window activation callback.
        using(var catalog=new CatalogService()){
            nint activated=0;
            var p=new Preferences{View="목록",GroupApps=false,ShowPreview=false,PeekTarget=false,HighlightTarget=false,EmphasizeTarget=false};
            using(var form=new SwitcherForm(p,catalog,h=>{activated=h;return true;}){InspectionMode=true,TopMost=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){
                form.PrepareNavigationTest(windows,pinned:false);form.Show();
                form.TestWheel(-120);
                nint wheelTarget=form.NavigationState.Selected;
                Check("plain AltTab wheel changes selected target",wheelTarget!=101&&wheelTarget!=0);
                Check("plain AltTab wheel never pins session",!form.NavigationState.Persistent);
                form.NavigationReleased();
                Check("plain AltTab wheel commits wheel-selected target on last release",activated==wheelTarget&&!form.Visible);
                form.Shutdown();
            }
            activated=0;
            using(var form=new SwitcherForm(p,catalog,h=>{activated=h;return true;}){InspectionMode=true,TopMost=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){
                form.PrepareNavigationTest(windows,pinned:true);form.Show();
                form.TestWheel(-120);
                Check("pinned AltTab wheel keeps persistent mode",form.NavigationState.Persistent);
                form.NavigationReleased();
                Check("pinned AltTab wheel does not auto-commit on key release",activated==0&&form.Visible);
                form.Shutdown();
            }
        }
        // The update UI must remain independently visible and dismissible. An
        // update check must not require ESC to unstick the Alt+Tab switcher.
        using(var updater=new UpdateWindow(()=>{}){
            StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),ShowInTaskbar=false
        }){
            updater.Show();
            Check("updater has visible persistent dialog",updater.Visible);
            Check("updater requires no token input",!updater.Controls.OfType<TextBox>().Any());
            Check("update action is focusable and enabled",updater.CheckUpdate.Enabled&&updater.CheckUpdate.CanFocus);
            updater.Close();
            Check("updater closes without trapping switcher",!updater.Visible);
        }
        // Synthetic maximized/topmost game overlay test: we can validate
        // Win32 Z-order on a visible desktop without claiming that a DXGI
        // exclusive-fullscreen/anti-cheat MapleStory session was exercised.
        var gameOrigin=new Form{ShowInTaskbar=false,TopMost=true,
            StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(70,80,440,290)};
        var overlayTest=new Form{ShowInTaskbar=false,TopMost=true,
            StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(160,120,250,190)};
        try{
            overlayTest.Show();gameOrigin.Show();
            Application.DoEvents();
            Check("maximized-game foreground reclaims overlay without immediate dismiss",
                SwitcherForm.ShouldKeepOverlayOnGameFocus(gameOrigin.Handle,gameOrigin.Handle));
            Check("unrelated foreground does not trigger game overlay protection",
                !SwitcherForm.ShouldKeepOverlayOnGameFocus(overlayTest.Handle,gameOrigin.Handle));
            Check("overlay topmost reassertion request succeeds",
                SwitcherForm.KeepOverlayAboveGame(overlayTest.Handle));
            Check("game overlay remains topmost without changing game band",
                overlayTest.TopMost&&gameOrigin.TopMost);
            Check("fullscreen activation window has bounded retry budget",
                WindowActivation.ActivationDeadlineMs>=500&&
                SwitcherForm.CommitDeadlineMs>=WindowActivation.ActivationDeadlineMs&&
                SwitcherForm.CommitDeadlineMs<=1500);
        }finally{overlayTest.Close();overlayTest.Dispose();gameOrigin.Close();gameOrigin.Dispose();}
        // Physical Alt+Tab bursts must allow Paint/Commit timers to run.
        Check("rapid AltTab commands are bounded per UI drain pass",
            InputDrainPolicy.MaxCommandsPerPass>=16&&InputDrainPolicy.MaxCommandsPerPass<=64);
        Check("rapid AltTab queue snapshots preserve bounded FIFO slices",
            InputDrainPolicy.SnapshotLimit(0)==InputDrainPolicy.MaxCommandsPerPass&&
            InputDrainPolicy.SnapshotLimit(InputDrainPolicy.MaxCommandsPerPass-1)==1&&
            InputDrainPolicy.SnapshotLimit(InputDrainPolicy.MaxCommandsPerPass)==0);
        Check("rapid AltTab drain yields for elapsed time or command count",
            !InputDrainPolicy.ShouldYield(0,TimeSpan.Zero)&&
            InputDrainPolicy.ShouldYield(InputDrainPolicy.MaxCommandsPerPass,TimeSpan.Zero)&&
            InputDrainPolicy.ShouldYield(1,TimeSpan.FromMilliseconds(InputDrainPolicy.MaxMillisecondsPerPass+1)));
        // MapleStory fullscreen focus regression: merely highlighting the
        // game's row must not promote its HWND or create extra topmost FX.
        var maple=new WindowInfo(8001,"MapleStory","MapleStory","MapleStory","C:/Games/MapleStory.exe",0,false);
        Check("MapleStory selected row skips external HWND Peek effects",
            SwitcherForm.ShouldAvoidGamePeek(maple));
        Check("MapleStory process match ignores case",
            SwitcherForm.ShouldAvoidGamePeek(maple with{ProcessName="maplestory",AppName="Game"}));
        Check("Chrome preview still retains existing Peek behavior",
            !SwitcherForm.ShouldAvoidGamePeek(windows[0]));
        Check("normal Alt+Tab survives temporary game focus loss while Alt held",
            SwitcherForm.ShouldRetainOnDeactivate(false,true,false,false));
        Check("recent normal Alt+Tab survives transient focus transition",
            SwitcherForm.ShouldRetainOnDeactivate(false,false,false,true));
        Check("pinned switcher does not retain focus after unrelated app activation",
            !SwitcherForm.ShouldRetainOnDeactivate(true,false,false,false));
        Check("return to origin foreground retains visible chooser",
            SwitcherForm.ShouldRetainOnDeactivate(false,false,true,false));
        foreach(float height in new[]{580f,650f,735f,950f}){
            var sidebar=SwitcherForm.LayoutPreviewSidebar(height);
            Check($"preview text never overlaps action button at logical height {height}",
                sidebar.ImageHeight>0&&sidebar.AppY+25<sidebar.TitleY&&
                sidebar.TitleY+sidebar.TitleHeight<=sidebar.ButtonY-11);
        }
        using(var catalog=new CatalogService())foreach(string view in new[]{"목록","카드","아이콘"}){
            nint activated=0;var prefs=new Preferences{View=view,ColorPreset="rose",ShowPreview=false,HighlightTarget=false,PeekTarget=false,EmphasizeTarget=false};
            using var form=new SwitcherForm(prefs,catalog,h=>{activated=h;return true;}){InspectionMode=true};
            form.PrepareNavigationTest(windows);using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,$"switcher-{view}.png"),ImageFormat.Png);
            var r=form.TestEntryBounds(0);var point=new Point(r.Left+12,r.Top+r.Height/2);form.TestMouse(point,true);Check(view+" mouse down does not activate prematurely",activated==0);form.TestMouse(Point.Empty,false);Check(view+" releasing outside cancels click",activated==0);form.TestMouse(point,true);form.TestMouse(point,false);Check(view+" releasing inside activates selected window",activated==101);form.Shutdown();
        }
        var report=new{failures,tests,dpi,previewDirectory=output};File.WriteAllText(Path.Combine(Preferences.DataDirectory,"ui-test.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Environment.ExitCode=failures==0?0:1;
    }
}