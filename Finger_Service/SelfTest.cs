namespace CJFingerService;
internal static class SelfTest {
    public static void StartupTest(bool startMacro=false,string? control=null) {
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CJ_FINGER_SERVICE_DATA")))throw new InvalidOperationException("Set an isolated CJ_FINGER_SERVICE_DATA directory for the startup test.");
        var report=Path.Combine(Settings.Root,control is not null?control.ToLowerInvariant()+"-result.txt":startMacro?"start-result.txt":"startup-result.txt");
        // Isolated test root only: emulate a legacy installation that used to auto-start.
        Directory.CreateDirectory(Path.Combine(Settings.Root,"exports"));
        var settings=new Settings {ProgramPath=Environment.ProcessPath!,ExportDirectory=Path.Combine(Settings.Root,"exports"),Username="test-user",PasswordProtected=Settings.Protect("test-password"),ScheduleStartAt=DateTime.Now.AddMinutes(-5)};
        var json=System.Text.Json.JsonSerializer.Serialize(settings);
        File.WriteAllText(Settings.ConfigPath,json[..^1]+",\"AutoStartSchedule\":true}");
        using var fixture=startMacro?System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) {Arguments="--demo-target",UseShellExecute=false}):null;
        using var app=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) {Arguments="--isolated-test-tray",UseShellExecute=false})!;
        try {
            Task.Run(()=> {
                IntPtr handle=IntPtr.Zero;
                for(var attempt=0;attempt<60 && handle==IntPtr.Zero;attempt++) {Thread.Sleep(250);handle=Native.FindWindow(app.Id,"CJ Finger Service");}
                if(handle==IntPtr.Zero)throw new Exception("Settings did not open; close other tray instances before this test.");
                var window=System.Windows.Automation.AutomationElement.FromHandle(handle);
                System.Windows.Automation.AutomationElement[] controls=[];
                for(var attempt=0;attempt<40;attempt++) {
                    controls=window.FindAll(System.Windows.Automation.TreeScope.Descendants,System.Windows.Automation.Condition.TrueCondition).Cast<System.Windows.Automation.AutomationElement>().ToArray();
                    if(controls.Any(e=>e.Current.Name=="Start date/time") && controls.Any(e=>e.Current.Name=="Save"))break;
                    Thread.Sleep(250);
                }
                if(controls.Any(e=>System.Text.RegularExpressions.Regex.IsMatch(e.Current.Name,"[\\u0e00-\\u0e7f]")))throw new Exception("Non-English UI text found.");
                if(!controls.Any(e=>e.Current.Name=="Start date/time") || controls.Any(e=>e.Current.Name=="Run now")) {
                    Native.Screenshot(handle,Path.Combine(Settings.Root,"startup-preview.png"));
                    throw new Exception("Unexpected schedule UI: "+string.Join(" | ",controls.Where(e=>!e.Current.IsPassword).Select(e=>e.Current.ControlType.ProgrammaticName+":"+e.Current.Name)));
                }
                var save=controls.Single(e=>e.Current.ControlType==System.Windows.Automation.ControlType.Button && e.Current.Name==(startMacro?"Start":"Save"));
                Native.PostMessage((IntPtr)save.Current.NativeWindowHandle,0x00F5,IntPtr.Zero,IntPtr.Zero);
                for(var attempt=0;attempt<20 && Native.FindWindow(app.Id,"CJ Finger Service")!=IntPtr.Zero;attempt++)Thread.Sleep(250);
                if(Native.FindWindow(app.Id,"CJ Finger Service")!=IntPtr.Zero)throw new Exception("Save did not close Settings.");
                if(control is not null) {
                    var stageFile=Path.Combine(Settings.Root,"worker-stage.txt");
                    for(var attempt=0;attempt<90;attempt++){if(File.Exists(stageFile)&&File.ReadAllText(stageFile).StartsWith("download"))break;Thread.Sleep(500);}
                    if(!File.Exists(stageFile)||!File.ReadAllText(stageFile).StartsWith("download"))throw new Exception("Worker did not reach the simulated stuck download.");
                    var taskWindow=System.Windows.Automation.AutomationElement.FromHandle(Native.FindWindow(app.Id,"Active task"));
                    var button=taskWindow.FindAll(System.Windows.Automation.TreeScope.Descendants,new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty,System.Windows.Automation.ControlType.Button)).Cast<System.Windows.Automation.AutomationElement>().Single(e=>e.Current.Name==control);
                    Native.PostMessage((IntPtr)button.Current.NativeWindowHandle,0x00F5,IntPtr.Zero,IntPtr.Zero);
                    if(control=="Exit") {
                        Thread.Sleep(500);
                        var confirm=System.Windows.Automation.AutomationElement.FromHandle(Native.FindWindow(app.Id,"CJ Finger Service"));
                        var yes=confirm.FindAll(System.Windows.Automation.TreeScope.Descendants,new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty,System.Windows.Automation.ControlType.Button)).Cast<System.Windows.Automation.AutomationElement>().Single(e=>e.Current.Name.Replace("&","")=="Yes");
                        Native.PostMessage((IntPtr)yes.Current.NativeWindowHandle,0x00F5,IntPtr.Zero,IntPtr.Zero);
                        if(!app.WaitForExit(5000))throw new Exception("Exit left the tray process running.");
                    } else {
                        var logFile=Path.Combine(Settings.Root,"service.log");
                        for(var attempt=0;attempt<20 && !File.ReadAllText(logFile).Contains("CANCELLED by user");attempt++)Thread.Sleep(250);
                        if(!File.ReadAllText(logFile).Contains("CANCELLED by user"))throw new Exception("Cancel did not stop the task.");
                        if(app.HasExited)throw new Exception("Cancel unexpectedly exited the tray app.");
                    }
                    var workerId=int.Parse(File.ReadAllText(Path.Combine(Settings.Root,"worker.pid")));
                    try {using var remaining=System.Diagnostics.Process.GetProcessById(workerId);if(!remaining.HasExited)throw new Exception("Worker still alive after "+control);}catch(ArgumentException){}
                    if(fixture!.HasExited)throw new Exception("Target exporter was closed by "+control);
                    return;
                }
                if(startMacro) {
                    var resultPath=Path.Combine(Settings.Root,"worker-result.json");
                    for(var attempt=0;attempt<120 && !File.Exists(resultPath);attempt++)Thread.Sleep(500);
                    if(!File.Exists(resultPath))throw new Exception("Start did not complete a worker run.");
                    using var result=System.Text.Json.JsonDocument.Parse(File.ReadAllText(resultPath));
                    if(!result.RootElement.GetProperty("ok").GetBoolean())throw new Exception("Start worker failed: "+result.RootElement.GetProperty("message").GetString());
                    return;
                }
                Thread.Sleep(12000);
                var log=Path.Combine(Settings.Root,"service.log");
                if(File.Exists(Path.Combine(Settings.Root,"worker-result.json")) || File.Exists(Path.Combine(Settings.Root,"worker-stage.txt")) || (File.Exists(log)&&File.ReadAllText(log).Contains("TASK START")))throw new Exception("Automation started without Start.");
                if(File.ReadAllText(Settings.ConfigPath).Contains("AutoStartSchedule"))throw new Exception("Legacy auto-start flag was retained.");
            }).GetAwaiter().GetResult();
            File.WriteAllText(report,control is not null?$"PASS: {control} stops the stuck worker, preserves the target exporter and leaves no worker process.":startMacro?"PASS: Start button with the default current time launches the macro and exports TXT successfully.":"PASS: English settings, legacy auto-start ignored, past date does not run, Save remains stopped.");
        }catch(Exception e){File.WriteAllText(report,"FAIL: "+e);Environment.ExitCode=1;}
        finally{if(!app.HasExited)app.Kill(true);if(fixture is not null && !fixture.HasExited)fixture.Kill(true);}
    }
    public static void AutomationTest() {
        var report=Path.Combine(Settings.Root,"automation-result.txt");
        Directory.CreateDirectory(Path.Combine(Settings.Root,"exports"));
        var target=Environment.GetEnvironmentVariable("CJ_FINGER_TEST_TARGET") ?? Environment.ProcessPath!;
        using var fixture=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { Arguments="--demo-target",UseShellExecute=false,CreateNoWindow=true })!;
        try {
            Thread.Sleep(2000);
            var settings=new Settings { ProgramPath=target,ExportDirectory=Path.Combine(Settings.Root,"exports"),Username="test-user",PasswordProtected=Settings.Protect("test-password"),LookbackDays=3,DownloadTimeoutSeconds=30 };
            settings.UseUbuntuOcr=Environment.GetEnvironmentVariable("CJ_FINGER_TEST_WINE")=="1";
            var stages=new List<string>();
            string file;
            try {
                file=Task.Run(()=>new ExportAutomation(settings,stage=> { stages.Add(stage); File.WriteAllText(Path.Combine(Settings.Root,"test-stage.txt"),stage); },CancellationToken.None).Run()).GetAwaiter().GetResult();
            } catch(InvalidOperationException e) when(Environment.GetEnvironmentVariable("CJ_FINGER_DEMO_LOGIN")=="always-fail" && e.Message.StartsWith("LOGIN_FAILED:")) {
                if(File.ReadAllText(Path.Combine(Settings.Root,"fixture-login-count.txt"))!="3" || stages.Contains("select_dates") || Directory.GetFiles(settings.ExportDirectory).Length!=0)
                    throw new Exception("Login failure did not stop after exactly three submissions.");
                File.WriteAllText(report,"PASS: login failure stopped after three submissions without selecting a session or exporting.");return;
            }
            if(Environment.GetEnvironmentVariable("CJ_FINGER_DEMO_LOGIN")=="always-fail")throw new Exception("Repeated login failure unexpectedly succeeded.");
            if(File.Exists(Path.Combine(Settings.Root,"invalid-login-submit.txt")))throw new Exception("Unverified credentials reached submit.");
            var loginMode=Environment.GetEnvironmentVariable("CJ_FINGER_DEMO_LOGIN");
            if(loginMode is "focus" or "clear") {
                if(!File.Exists(Path.Combine(Settings.Root,"login-disturbed.txt")) || !stages.Any(s=>s.StartsWith("Retry 2/3: login")))throw new Exception("Injected input failure did not trigger credential refill.");
            }
            if(loginMode is "reject" or "modal" && File.ReadAllText(Path.Combine(Settings.Root,"fixture-login-count.txt"))!="2")throw new Exception("Login rejection was not retried exactly once.");
            var today=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"SE Asia Standard Time").Date;
            var content=File.ReadAllText(file);
            if(File.Exists(Path.Combine(Settings.Root,"early-export.txt")))throw new Exception("Export clicked before download completion.");
            if(Environment.GetEnvironmentVariable("CJ_FINGER_DEMO_RETRY")!="1" && File.ReadAllText(Path.Combine(Settings.Root,"fixture-download-count.txt"))!="1")throw new Exception("Active download was restarted.");
            if(!content.Contains(today.AddDays(-3).ToString("yyyyMMdd")) || !content.Contains(today.ToString("yyyyMMdd"))) throw new Exception("Date range mismatch");
            var folderChanged=Environment.GetEnvironmentVariable("CJ_FINGER_DEMO_FOLDER_CHANGE")=="1";
            var saveCountPath=Path.Combine(Settings.Root,"fixture-folder-save-count.txt");
            var saveCount=File.Exists(saveCountPath)?int.Parse(File.ReadAllText(saveCountPath)):0;
            if(saveCount!=(folderChanged?1:0))throw new Exception("Directory settings were saved unnecessarily or were not saved after a change.");
            var expected=folderChanged?new[]{"login","select_dates","save_directory","download","export"}:new[]{"login","select_dates","download","export"};
            if(!stages.Where(s=>!s.StartsWith("Retry ") && !s.StartsWith("download:")).SequenceEqual(expected)) throw new Exception("Unexpected stage order");
            File.WriteAllText(report,$"PASS: {(settings.UseUbuntuOcr?"Wine native":"UI Automation")} login, session, 3-day dates, wait ready, TXT export and validation.\n"+file);
        } catch(Exception e) { File.WriteAllText(report,"FAIL: "+e); try { fixture.Refresh(); Native.Screenshot(fixture.MainWindowHandle,Path.Combine(Settings.Root,"fixture-failure.png")); } catch { } Environment.ExitCode=1; }
        finally { if(!fixture.HasExited) fixture.Kill(true); }
    }
    public static void Run() {
        WineTests.Run();
        DownloadProgressTests.Run();
        UpdateTests.Run();
        var now=DateTimeOffset.Now;
        foreach(var value in new[]{new Settings()}) {
            var blocked=false;try {_=TrayApp.FirstRun(value,now);}catch(InvalidOperationException){blocked=true;}
            if(!blocked)throw new Exception("Missing start time was accepted.");
        }
        foreach(var date in new[]{now.LocalDateTime,now.LocalDateTime.AddMinutes(-1)})if(TrayApp.FirstRun(new Settings {ScheduleStartAt=date},now)!=now)throw new Exception("Current/past start should begin on explicit Start.");
        if(TrayApp.FirstRun(new Settings {ScheduleStartAt=now.LocalDateTime.AddMinutes(10)},now)!=now.AddMinutes(10))throw new Exception("Future schedule calculation failed.");
        var directory=Path.Combine(Settings.Root,"self-test"); Directory.CreateDirectory(directory);
        var good=Path.Combine(directory,"good.txt"); File.WriteAllText(good,"BE000609\t20260929\t0500\nBE000609\t20260928\t1641\n");
        ExportAutomation.ValidateFile(good);
        var bad=Path.Combine(directory,"bad.txt"); File.WriteAllText(bad,"BE000609 20260230 2500");
        var rejected=false; try { ExportAutomation.ValidateFile(bad); } catch(InvalidOperationException) { rejected=true; }
        if(!rejected) throw new Exception("Invalid date was accepted.");
        if(Settings.Unprotect(Settings.Protect("self-test-value"))!="self-test-value") throw new Exception("DPAPI round trip failed.");
        File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: current/past Start begins now, future Start waits, valid TXT, invalid TXT rejection, DPAPI round trip.");
    }
}
