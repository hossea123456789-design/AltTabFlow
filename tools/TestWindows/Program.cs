using System.Drawing;
using System.Windows.Forms;
class TestApp {
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowWindow(nint hwnd,int command);
 [STAThread] static void Main() {
  Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
  var context=new ApplicationContext(); int remaining=2;
  foreach(string name in new[]{"A","B"}) {
   var form=new Form{Text="AltTabFlow 테스트 "+name,Size=new Size(420,220),StartPosition=FormStartPosition.Manual,Location=new Point(name=="A"?100:550,150)};
   form.Controls.Add(new Label{Text="창 전환 검증용 "+name+"\n사용자 문서와 무관한 임시 창입니다.",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("맑은 고딕",14)});
   form.Activated+=(_,_)=>File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AltTabFlow","fixture-focus.log"),DateTimeOffset.Now.ToString("O")+" "+name+Environment.NewLine);
   form.FormClosed+=(_,_)=>{if(--remaining==0)context.ExitThread();};
   form.Show(); ShowWindow(form.Handle,5);
  }
  Application.Run(context);
 }
}
