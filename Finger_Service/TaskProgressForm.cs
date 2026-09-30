using System.Drawing;
using Forms=System.Windows.Forms;
namespace CJFingerService;
internal sealed class TaskProgressForm:Forms.Form {
    private readonly Forms.Timer refresh=new() {Interval=1000};
    private readonly Forms.Label status=new() {Dock=Forms.DockStyle.Fill,Text="Starting...",Padding=new Forms.Padding(12),AutoEllipsis=true};
    internal TaskProgressForm(Action cancel,Action exit) {
        Text="Finger Service - Active task";ClientSize=new Size(390,115);Font=new Font("Segoe UI",10);StartPosition=Forms.FormStartPosition.CenterScreen;
        var button=new Forms.Button {Text="Cancel",Dock=Forms.DockStyle.Bottom,Height=34,BackColor=Color.MistyRose};
        var exitButton=new Forms.Button {Text="Exit",Dock=Forms.DockStyle.Bottom,Height=30};
        exitButton.Click+=(_,_)=>exit();
        button.Click+=(_,_)=> {button.Enabled=false;status.Text="Cancelling...";cancel();};
        Controls.Add(status);Controls.Add(button);Controls.Add(exitButton);
        refresh.Tick+=(_,_)=> {try {var path=Path.Combine(Settings.Root,"worker-stage.txt");if(button.Enabled && File.Exists(path)){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var reader=new StreamReader(stream);var text=reader.ReadToEnd();if(text.Length>0)status.Text=text;}}catch(IOException){}};
        refresh.Start();
        FormClosing+=(_,_)=>cancel();
        FormClosed+=(_,_)=> {refresh.Stop();refresh.Dispose();};
    }
}
