using System.Drawing;
using Forms=System.Windows.Forms;
namespace CJFingerService;

internal sealed class UpdateProgressForm : Forms.Form {
    private readonly Forms.Label status=new() {Dock=Forms.DockStyle.Top,Height=48,Text="Preparing update..."};
    private readonly Forms.ProgressBar bar=new() {Dock=Forms.DockStyle.Top,Height=24};
    internal UpdateProgressForm() {
        Text="Finger Service - Update";ClientSize=new Size(440,105);
        Padding=new Forms.Padding(18);Font=new Font("Segoe UI",10);
        StartPosition=Forms.FormStartPosition.CenterScreen;FormBorderStyle=Forms.FormBorderStyle.FixedDialog;
        MaximizeBox=false;MinimizeBox=false;ControlBox=false;ShowInTaskbar=true;
        Controls.Add(bar);Controls.Add(status);
    }
    internal void Report(int percent,string message) {
        if(IsDisposed)return;
        bar.Value=Math.Clamp(percent,0,100);status.Text=$"{message}  ({bar.Value}%)";
    }
}
