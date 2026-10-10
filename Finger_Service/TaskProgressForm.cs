using System.Diagnostics;
using System.Drawing;
using Forms=System.Windows.Forms;
namespace CJFingerService;
internal sealed class TaskProgressForm:Forms.Form {
    private readonly Forms.Timer refresh=new() {Interval=1000};
    private readonly Forms.Label heading=new() {Dock=Forms.DockStyle.Top,Height=34,AutoEllipsis=true,Font=new Font("Segoe UI",11,FontStyle.Bold)};
    private readonly Forms.Label elapsed=new() {Dock=Forms.DockStyle.Top,Height=28};
    private readonly Forms.TextBox status=new() {Dock=Forms.DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=Forms.ScrollBars.Vertical,BackColor=Color.White,BorderStyle=Forms.BorderStyle.None};
    private readonly Stopwatch total=Stopwatch.StartNew(),stageTime=Stopwatch.StartNew();
    private string last="Starting...",code="START",updatedAt="Waiting for worker";
    private bool finished;
    private readonly Forms.Button cancelButton=new() {Text="Cancel",Width=75,Height=28,BackColor=Color.MistyRose};
    protected override bool ShowWithoutActivation=>true;
    protected override Forms.CreateParams CreateParams {get {var p=base.CreateParams;p.ExStyle|=0x08000000;return p;}}
    internal TaskProgressForm(Action cancel,Action exit) {
        Text="Finger Service - Active task";ClientSize=new Size(460,260);Font=new Font("Segoe UI",9);
        StartPosition=Forms.FormStartPosition.Manual;FormBorderStyle=Forms.FormBorderStyle.FixedToolWindow;Padding=new Forms.Padding(12);
        var area=Forms.Screen.PrimaryScreen!.WorkingArea;
        Location=new Point(area.Right-Width-12,area.Bottom-Height-12);
        var buttons=new Forms.FlowLayoutPanel {Dock=Forms.DockStyle.Bottom,Height=34,FlowDirection=Forms.FlowDirection.RightToLeft};
        var copy=new Forms.Button {Text="Copy status",Width=100,Height=28};
        copy.Click+=(_,_)=> {try{Forms.Clipboard.SetText($"Finger Service {Updater.VersionText}\n{heading.Text}\n{elapsed.Text}\n{status.Text}");}catch(Exception){Forms.MessageBox.Show("Clipboard unavailable. Take a screenshot or open service.log.");}};
        var exitButton=new Forms.Button {Text="Exit",Width=75,Height=28};exitButton.Click+=(_,_)=>exit();
        cancelButton.Click+=(_,_)=> {if(finished){Close();return;}cancelButton.Enabled=false;heading.Text="Cancelling...";cancel();};
        buttons.Controls.AddRange([copy,cancelButton,exitButton]);
        Controls.Add(status);Controls.Add(elapsed);Controls.Add(heading);Controls.Add(buttons);
        ReadStage();refresh.Tick+=(_,_)=>ReadStage();refresh.Start();
        FormClosing+=(_,_)=>{if(!finished)cancel();};
        FormClosed+=(_,_)=> {refresh.Stop();refresh.Dispose();};
    }
    private void ReadStage() {
        if(finished)return;
        try {
            var path=Path.Combine(Settings.Root,"worker-stage.txt");
            if(File.Exists(path)) {
                using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var reader=new StreamReader(stream);
                var text=reader.ReadToEnd();
                if(text.Length>0) {
                    var stage=TaskStage.Describe(text);
                    if(stage.Code!=code && !text.StartsWith("Retry")){code=stage.Code;stageTime.Restart();}
                    last=text;updatedAt=File.GetLastWriteTime(path).ToString("HH:mm:ss");
                    if(!text.StartsWith("Retry"))heading.Text=$"[{code}] {stage.Title}";
                    var display=last.Replace("; ",Environment.NewLine);
                    if(status.Text!=display)status.Text=display;
                }
            }
        }catch(IOException){}
        elapsed.Text=$"Step: {stageTime.Elapsed:mm\\:ss} | Total: {total.Elapsed:mm\\:ss} | Updated: {updatedAt}";
    }
    internal void Complete(string outcome,bool failed) {
        ReadStage();finished=true;refresh.Stop();total.Stop();stageTime.Stop();
        heading.ForeColor=failed?Color.Firebrick:Color.ForestGreen;
        heading.Text=(failed?"Stopped at ":"Completed after ")+"["+code+"]";
        status.Text=outcome+Environment.NewLine+Environment.NewLine+"Last worker status:"+Environment.NewLine+last.Replace("; ",Environment.NewLine);
        cancelButton.Text="Close";cancelButton.Enabled=true;
    }
}
