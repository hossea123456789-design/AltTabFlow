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
        using(var form=new Form{StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),ShowInTaskbar=false})using(var selector=new FlowSelect{Palette=ThemePalette.All[3],Width=272,Height=40}){
            form.Controls.Add(selector);form.CreateControl();selector.CreateControl();selector.Items.AddRange(new object[]{"목록","카드","아이콘"});selector.SelectedIndex=0;
            var menu=selector.TestMenu();Check("selector opens a themed native menu",menu.Visible&&menu.Items.Count==3&&menu.BackColor==ThemePalette.All[3].Surface);
            using(var bitmap=new Bitmap(menu.Width,menu.Height)){menu.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"selector-rose.png"),ImageFormat.Png);}
            menu.Items[2].PerformClick();menu.Close();Check("menu selection updates selected view",Equals(selector.SelectedItem,"아이콘"));bool reusable=true;for(int i=0;i<10;i++){var next=selector.TestMenu();next.Close(ToolStripDropDownCloseReason.Keyboard);reusable&=ReferenceEquals(next,menu)&&!next.IsDisposed;}Check("selector menu survives repeated close and reopen",reusable);
        }
        using(var list=new FlowAppList()){
            list.Items.AddRange(new object[]{"Chrome","Slack"});list.SelectedIndex=0;list.TestKey(Keys.Space);Check("Space registers selected app",list.CheckedItems.SequenceEqual(new object[]{"Chrome"}));Check("app registration exposed to accessibility",(list.AccessibilityObject.GetChild(0)!.State&AccessibleStates.Checked)!=0);list.TestKey(Keys.Space);Check("Space unregisters selected app",!list.CheckedItems.Any());
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