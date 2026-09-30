using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using Forms = System.Windows.Forms;
namespace CJFingerService;
internal sealed class TrayApp : Forms.ApplicationContext {
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.Timer timer=new() { Interval=1000 };
    private readonly Forms.ToolStripMenuItem status=new("พร้อมตั้งค่า / Ready"), run=new("ดึงข้อมูลตอนนี้ / Run now"), pause=new("เริ่มรอบอัตโนมัติ / Start schedule");
    private DateTimeOffset next=DateTimeOffset.Now;
    private bool scheduled, running;
    private bool updating;
    private bool exiting;
    private readonly Forms.Timer updateTimer=new() {Interval=12*60*60*1000};
    private readonly Forms.ToolStripMenuItem checkUpdate=new("ตรวจอัปเดต / Check updates");
    private Process? worker;
    private Settings settings=Settings.Load();
    public TrayApp() {
        tray=new Forms.NotifyIcon { Icon=SystemIcons.Application, Text="CJ Finger Service · 30 min", Visible=true };
        var menu=new Forms.ContextMenuStrip(); status.Enabled=false;
        menu.Items.Add(status); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add(run); menu.Items.Add(pause);
        menu.Items.Add("ตั้งค่า / Settings",null,(_,_)=>OpenSettings());
        menu.Items.Add(new Forms.ToolStripMenuItem("Version "+Updater.VersionText) {Enabled=false});
        menu.Items.Add(checkUpdate); checkUpdate.Click+=async(_,_)=>await CheckUpdates(true);
        updateTimer.Tick+=async(_,_)=>await CheckUpdates(false); updateTimer.Start();
        var startupCheck=new Forms.Timer {Interval=10000};
        startupCheck.Tick+=async(_,_)=> {startupCheck.Stop();startupCheck.Dispose();await CheckUpdates(false);}; startupCheck.Start();
        menu.Items.Add("ส่งไฟล์ที่ค้าง / Retry uploads",null,async(_,_)=>await Run(true));
        menu.Items.Add("เปิดไฟล์ที่เก็บไว้ / Open exports",null,(_,_)=> { Directory.CreateDirectory(Path.Combine(Settings.Root,"pending")); Process.Start(new ProcessStartInfo("explorer.exe") { Arguments=Path.Combine(Settings.Root,"pending"),UseShellExecute=true }); });
        menu.Items.Add("เปิดบันทึกการทำงาน / Open logs",null,(_,_)=>Process.Start(new ProcessStartInfo("explorer.exe") { Arguments=Settings.Root,UseShellExecute=true }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("ออก / Exit",null,(_,_)=>Exit());
        tray.ContextMenuStrip=menu; tray.DoubleClick+=(_,_)=>OpenSettings();
        run.Click+=async(_,_)=>await Run();
        pause.Click+=(_,_)=> {
            if(!scheduled) { try { settings.Validate(false); } catch(Exception e) { Forms.MessageBox.Show(e.Message); return; } }
            scheduled=!scheduled; settings.AutoStartSchedule=scheduled; settings.Save(); next=FirstRun(settings,DateTimeOffset.Now); ArmTimer(); UpdateStatus(scheduled?"Schedule started":"Schedule paused");
        };
        timer.Tick+=async(_,_)=> { timer.Stop(); if(scheduled && !running && DateTimeOffset.Now>=next) await Run(); else ArmTimer(); };
        scheduled=settings.AutoStartSchedule; next=FirstRun(settings,DateTimeOffset.Now); ArmTimer();
        UpdateStatus("Ready · right-click tray icon to configure");
        if(string.IsNullOrEmpty(settings.ProgramPath)) OpenSettings();
    }
    private async Task CheckUpdates(bool interactive) {
        if(exiting || updating || running) { if(interactive) Forms.MessageBox.Show("Wait for the active task to finish.");return; }
        updating=true;checkUpdate.Enabled=false;timer.Stop();
        try {
            var release=await Updater.Check();
            if(release is null) {if(interactive) Forms.MessageBox.Show("No newer published release. Current: "+Updater.VersionText);return;}
            if(!interactive) {tray.ShowBalloonTip(5000,"Update available",$"Version {release.Version}. Use Check updates to install.",Forms.ToolTipIcon.Info);return;}
            if(Forms.MessageBox.Show($"Update {Updater.VersionText} → {release.Version}?\nThe app will restart. Settings and queued files are retained.","CJ Finger Service",Forms.MessageBoxButtons.YesNo)!=Forms.DialogResult.Yes) return;
            checkUpdate.Text="กำลังดาวน์โหลด / Downloading…";
            var source=await Updater.Prepare(release);
            if(exiting) return;
            Updater.Install(source); Exit();
        } catch(Exception e) {Log("UPDATE FAILED "+e.Message);if(interactive) Forms.MessageBox.Show(e.Message,"Update failed");}
        finally {updating=false;if(!exiting) {checkUpdate.Text="ตรวจอัปเดต / Check updates";checkUpdate.Enabled=true;ArmTimer();}}
    }
    internal static DateTimeOffset FirstRun(Settings value,DateTimeOffset now) => value.ScheduleStartAt is DateTime date && new DateTimeOffset(date)>now ? new DateTimeOffset(date) : now;
    private void ArmTimer() {
        timer.Stop();
        if(!scheduled || running || updating) return;
        timer.Interval=(int)Math.Clamp(Math.Ceiling((next-DateTimeOffset.Now).TotalMilliseconds),1,int.MaxValue);
        timer.Start();
    }
    private void UpdateStatus(string message) {
        pause.Text=scheduled?"หยุดรอบอัตโนมัติ / Pause schedule":"เริ่มรอบอัตโนมัติ / Start schedule";
        status.Text=scheduled && !running?$"{message} · Next {next:dd/MM HH:mm}":message; run.Enabled=!running;
        tray.Text=running?"CJ Finger Service · running":scheduled?$"CJ Finger Service · next {next:HH:mm}":"CJ Finger Service · paused";
    }
    private void Log(string message) { File.AppendAllText(Path.Combine(Settings.Root,"service.log"),$"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); }
    private async Task Run(bool uploadOnly=false) {
        if(running || updating) return;
        timer.Stop(); running=true; next=DateTimeOffset.Now.AddMinutes(30); UpdateStatus("กำลังดึงไฟล์ / Running");
        try {
            settings=Settings.Load(); if(!uploadOnly) settings.Validate(false);
            var resultPath=Path.Combine(Settings.Root,"worker-result.json");
            // Only the transient result file is replaced; source exports and pending files are retained.
            if(File.Exists(resultPath)) File.Delete(resultPath);
            var start=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,CreateNoWindow=true }; start.ArgumentList.Add(uploadOnly?"--upload-only":"--worker");
            worker=Process.Start(start)!;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(settings.DownloadTimeoutSeconds+180+ (settings.EnableUpload?480:0)));
            try { await worker.WaitForExitAsync(timeout.Token); } catch(OperationCanceledException) { worker.Kill(true); throw new TimeoutException("Automation timeout; no further clicks will be made. Check the export program."); }
            if(!File.Exists(resultPath)) throw new InvalidOperationException("Worker stopped without a result.");
            using var json=JsonDocument.Parse(File.ReadAllText(resultPath)); var message=json.RootElement.GetProperty("message").GetString()!;
            if(!json.RootElement.GetProperty("ok").GetBoolean()) throw new InvalidOperationException(message);
            Log("SUCCESS " + message + " " + json.RootElement.GetProperty("file").GetString());
            UpdateStatus(message);
            tray.ShowBalloonTip(4000,"CJ Finger Service",message,Forms.ToolTipIcon.Info);
        } catch(Exception e) { Log("FAILED " + e.Message); UpdateStatus("Failed · " + e.Message); tray.ShowBalloonTip(5000,"CJ Finger Service",e.Message,Forms.ToolTipIcon.Warning); }
        finally { worker?.Dispose(); worker=null; running=false; run.Enabled=true; if(next<=DateTimeOffset.Now) next=DateTimeOffset.Now.AddMinutes(30); ArmTimer(); if(scheduled) tray.Text=$"CJ Finger Service · next {next:HH:mm}"; }
    }
    private void OpenSettings() {
        if(updating) return;
        if(running) { tray.ShowBalloonTip(3000,"CJ Finger Service","Wait for the active run to finish, or exit to stop the worker.",Forms.ToolTipIcon.Info); return; }
        using var form=new SettingsForm(settings);
        timer.Stop();
        if(form.ShowDialog()==Forms.DialogResult.OK) { settings=form.Value; scheduled=settings.AutoStartSchedule; next=FirstRun(settings,DateTimeOffset.Now); UpdateStatus("Settings saved"); }
        ArmTimer();
    }
    private void Exit() {
        if(running && Forms.MessageBox.Show("หยุดมาโครที่กำลังทำงานและออกจากโปรแกรม?", "CJ Finger Service",Forms.MessageBoxButtons.YesNo)!=Forms.DialogResult.Yes) return;
        exiting=true;
        scheduled=false; timer.Stop(); if(worker is { HasExited:false }) worker.Kill(true);
        updateTimer.Stop(); updateTimer.Dispose(); tray.Visible=false; tray.Dispose(); timer.Dispose(); ExitThread();
    }
}
