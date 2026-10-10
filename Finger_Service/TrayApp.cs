using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using Forms = System.Windows.Forms;
namespace CJFingerService;
internal sealed class TrayApp : Forms.ApplicationContext {
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.Timer timer=new() { Interval=1000 };
    private readonly Forms.ToolStripMenuItem status=new("Stopped"), pause=new("Set start time...");
    private DateTimeOffset next=ServiceClock.Now;
    private bool scheduled=false, running;
    private bool updating;
    private bool exiting;
    private readonly Forms.Timer updateTimer=new() {Interval=12*60*60*1000};
    private readonly Forms.ToolStripMenuItem checkUpdate=new("Check updates");
    private Process? worker;
    private CancellationTokenSource? activeTask;
    private TaskProgressForm? progress;
    private readonly Forms.ToolStripMenuItem cancelTask=new("Cancel current task") {Enabled=false};
    private Settings settings=Settings.Load();
    public TrayApp() {
        tray=new Forms.NotifyIcon { Icon=SystemIcons.Application, Text="CJ Finger Service · 30 min", Visible=true };
        var menu=new Forms.ContextMenuStrip(); status.Enabled=false;
        menu.Items.Add(status); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add(pause);
        menu.Items.Add("Settings",null,(_,_)=>OpenSettings());
        menu.Items.Add("Show task status",null,(_,_)=> {if(progress is {IsDisposed:false}) {progress.Show();progress.BringToFront();}else Forms.MessageBox.Show("No task status yet. Click Start to run a task.");});
        menu.Items.Add(cancelTask);cancelTask.Click+=(_,_)=>CancelActive();
        menu.Items.Add(new Forms.ToolStripMenuItem("Version "+Updater.VersionText) {Enabled=false});
        menu.Items.Add(checkUpdate); checkUpdate.Click+=async(_,_)=>await CheckUpdates(true);
        updateTimer.Tick+=async(_,_)=>await CheckUpdates(false); updateTimer.Start();
        var startupCheck=new Forms.Timer {Interval=10000};
        startupCheck.Tick+=async(_,_)=> {startupCheck.Stop();startupCheck.Dispose();await CheckUpdates(false);}; startupCheck.Start();
        menu.Items.Add("Retry uploads",null,async(_,_)=>await Run(true));
        menu.Items.Add("Open exports",null,(_,_)=> { Directory.CreateDirectory(Path.Combine(Settings.Root,"pending")); Process.Start(new ProcessStartInfo("explorer.exe") { Arguments=Path.Combine(Settings.Root,"pending"),UseShellExecute=true }); });
        menu.Items.Add("Open logs",null,(_,_)=>Process.Start(new ProcessStartInfo("explorer.exe") { Arguments=Settings.Root,UseShellExecute=true }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit",null,(_,_)=>Exit());
        tray.ContextMenuStrip=menu; tray.DoubleClick+=(_,_)=>OpenSettings();
        pause.Click+=(_,_)=> {
            if(!scheduled) {OpenSettings();return;}
            scheduled=false; ArmTimer(); UpdateStatus("Schedule stopped");
        };
        timer.Tick+=async(_,_)=> { timer.Stop(); if(scheduled && !running && ServiceClock.Now>=next) await Run(); else ArmTimer(); };
        UpdateStatus("Stopped - choose a start time and click Start");
        OpenSettings();
    }
    private async Task CheckUpdates(bool interactive) {
        if(exiting || updating || running) { if(interactive) Forms.MessageBox.Show("Wait for the active task to finish.");return; }
        updating=true;checkUpdate.Enabled=false;timer.Stop();
        try {
            var release=await Updater.Check();
            if(release is null) {if(interactive) Forms.MessageBox.Show("No newer published release. Current: "+Updater.VersionText);return;}
            if(!interactive) {tray.ShowBalloonTip(5000,"Update available",$"Version {release.Version}. Use Check updates to install.",Forms.ToolTipIcon.Info);return;}
            if(Forms.MessageBox.Show($"Update {Updater.VersionText} → {release.Version}?\nThe app will restart. Settings and queued files are retained.","CJ Finger Service",Forms.MessageBoxButtons.YesNo)!=Forms.DialogResult.Yes) return;
            checkUpdate.Text="Downloading...";
            using var updateProgress=new UpdateProgressForm();
            updateProgress.Show();updateProgress.Activate();
            var source=await Updater.Prepare(release,new Progress<(int Percent,string Message)>(p=>updateProgress.Report(p.Percent,p.Message)));
            if(exiting) return;
            updateProgress.Report(84,"Starting update helper");
            await Updater.Install(source); Exit();
        } catch(Exception e) {Log("UPDATE FAILED "+e.Message);if(interactive) Forms.MessageBox.Show(e.Message,"Update failed");}
        finally {updating=false;if(!exiting) {checkUpdate.Text="Check updates";checkUpdate.Enabled=true;ArmTimer();}}
    }
    internal static DateTimeOffset FirstRun(Settings value,DateTimeOffset now) {
        if(value.ScheduleStartAt is not DateTime date) throw new InvalidOperationException("Choose a start date and time before clicking Start.");
        var selected=ServiceClock.Scheduled(date);
        return selected>now?selected:now;
    }
    private void ArmTimer() {
        timer.Stop();
        if(!scheduled || running || updating) return;
        timer.Interval=(int)Math.Clamp(Math.Ceiling((next-ServiceClock.Now).TotalMilliseconds),1,int.MaxValue);
        timer.Start();
    }
    private void UpdateStatus(string message) {
        pause.Text=scheduled?"Stop schedule":"Set start time...";
        status.Text=scheduled && !running?$"{message} · Next {next:dd/MM HH:mm} UTC+07":message;
        tray.Text=running?"CJ Finger Service · running":scheduled?$"CJ Finger Service · next {next:HH:mm} UTC+07":"CJ Finger Service · paused";
    }
    private void Log(string message) { File.AppendAllText(Path.Combine(Settings.Root,"service.log"),$"{ServiceClock.Now:O} {message}{Environment.NewLine}"); }
    private async Task Run(bool uploadOnly=false) {
        if(running || updating) return;
        Log(uploadOnly?"TASK START upload retry":"TASK START scheduled export");
        timer.Stop(); running=true; next=ServiceClock.Now.AddMinutes(30); UpdateStatus("Running");
        activeTask=new CancellationTokenSource();cancelTask.Enabled=true;
        var stagePath=Path.Combine(Settings.Root,"worker-stage.txt");File.WriteAllText(stagePath,"Starting...");
        progress?.Dispose();progress=new TaskProgressForm(()=> {if(running)CancelActive();},Exit);progress.Show();
        var outcome="Task stopped";var failed=true;
        try {
            settings=Settings.Load(); if(!uploadOnly) settings.Validate(false);
            var resultPath=Path.Combine(Settings.Root,"worker-result.json");
            // Only the transient result file is replaced; source exports and pending files are retained.
            if(File.Exists(resultPath)) File.Delete(resultPath);
            var start=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,CreateNoWindow=true }; start.ArgumentList.Add(uploadOnly?"--upload-only":"--worker");
            worker=Process.Start(start)!;
            File.WriteAllText(Path.Combine(Settings.Root,"worker.pid"),worker.Id.ToString());
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(activeTask.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.DownloadTimeoutSeconds*3+360+ (settings.EnableUpload?480:0)));
            try { await worker.WaitForExitAsync(timeout.Token); } catch(OperationCanceledException) {
                if(!worker.HasExited){worker.Kill();worker.WaitForExit(3000);}
                if(activeTask.IsCancellationRequested)throw;
                throw new TimeoutException("Automation timeout; worker stopped. Inspect the export application.");
            }
            if(!File.Exists(resultPath)) throw new InvalidOperationException("Worker stopped without a result.");
            using var json=JsonDocument.Parse(File.ReadAllText(resultPath)); var message=json.RootElement.GetProperty("message").GetString()!;
            if(!json.RootElement.GetProperty("ok").GetBoolean()) throw new InvalidOperationException(message);
            Log("SUCCESS " + message + " " + json.RootElement.GetProperty("file").GetString());
            UpdateStatus(message);outcome=message;failed=false;
            tray.ShowBalloonTip(4000,"CJ Finger Service",message,Forms.ToolTipIcon.Info);
        } catch(OperationCanceledException) {outcome="Cancelled - schedule stopped";Log("CANCELLED by user");if(!exiting)UpdateStatus("Cancelled - schedule stopped");}
        catch(Exception e) { outcome=e.Message;Log("FAILED " + e.Message); if(!exiting){UpdateStatus("Failed · " + e.Message); tray.ShowBalloonTip(5000,"CJ Finger Service",e.Message,Forms.ToolTipIcon.Warning);} }
        finally {
            worker?.Dispose(); worker=null; running=false;activeTask?.Dispose();activeTask=null;
            if(!exiting && progress is {IsDisposed:false})progress.Complete(outcome,failed);
            if(!exiting){cancelTask.Enabled=false;if(next<=ServiceClock.Now) next=ServiceClock.Now.AddMinutes(30);ArmTimer();tray.Text=scheduled?$"CJ Finger Service · next {next:HH:mm} UTC+07":"CJ Finger Service · stopped";}
        }
    }
    private void CancelActive() {
        if(!running)return;
        scheduled=false;timer.Stop();activeTask?.Cancel();cancelTask.Enabled=false;
        UpdateStatus("Cancelling - schedule stopped");
    }
    private void OpenSettings() {
        if(updating) return;
        if(running) {progress?.Show();progress?.Activate();return;}
        using var form=new SettingsForm(settings);
        timer.Stop();
        if(form.ShowDialog()==Forms.DialogResult.OK) { settings=form.Value; scheduled=form.StartRequested; if(scheduled) next=FirstRun(settings,ServiceClock.Now); UpdateStatus(scheduled?"Schedule started":"Settings saved - stopped"); }
        ArmTimer();
    }
    private void Exit() {
        if(running && Forms.MessageBox.Show("Stop the active task and exit?", "CJ Finger Service",Forms.MessageBoxButtons.YesNo)!=Forms.DialogResult.Yes) return;
        exiting=true;
        scheduled=false; timer.Stop();activeTask?.Cancel();
        try {
            if(worker is { HasExited:false }) {worker.Kill();worker.WaitForExit(3000);}
            updateTimer.Stop(); updateTimer.Dispose(); tray.Visible=false; tray.Dispose(); timer.Dispose(); ExitThread();
        } finally {Environment.Exit(0);}
    }
}
