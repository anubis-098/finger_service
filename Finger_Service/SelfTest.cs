namespace CJFingerService;
internal static class SelfTest {
    public static void StartupTest() {
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CJ_FINGER_SERVICE_DATA")))throw new InvalidOperationException("Set an isolated CJ_FINGER_SERVICE_DATA directory for the startup test.");
        var report=Path.Combine(Settings.Root,"startup-result.txt");
        // Isolated test root only: emulate a legacy installation that used to auto-start.
        var settings=new Settings {ProgramPath=Environment.ProcessPath!,Username="test-user",PasswordProtected=Settings.Protect("test-password"),ScheduleStartAt=DateTime.Now.AddMinutes(-5)};
        var json=System.Text.Json.JsonSerializer.Serialize(settings);
        File.WriteAllText(Settings.ConfigPath,json[..^1]+",\"AutoStartSchedule\":true}");
        using var app=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) {UseShellExecute=false})!;
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
                var save=controls.Single(e=>e.Current.ControlType==System.Windows.Automation.ControlType.Button && e.Current.Name=="Save");
                Native.PostMessage((IntPtr)save.Current.NativeWindowHandle,0x00F5,IntPtr.Zero,IntPtr.Zero);
                for(var attempt=0;attempt<20 && Native.FindWindow(app.Id,"CJ Finger Service")!=IntPtr.Zero;attempt++)Thread.Sleep(250);
                if(Native.FindWindow(app.Id,"CJ Finger Service")!=IntPtr.Zero)throw new Exception("Save did not close Settings.");
                Thread.Sleep(12000);
                var log=Path.Combine(Settings.Root,"service.log");
                if(File.Exists(Path.Combine(Settings.Root,"worker-result.json")) || File.Exists(Path.Combine(Settings.Root,"worker-stage.txt")) || (File.Exists(log)&&File.ReadAllText(log).Contains("TASK START")))throw new Exception("Automation started without Start.");
                if(File.ReadAllText(Settings.ConfigPath).Contains("AutoStartSchedule"))throw new Exception("Legacy auto-start flag was retained.");
            }).GetAwaiter().GetResult();
            File.WriteAllText(report,"PASS: English settings, legacy auto-start ignored, past date does not run, Save remains stopped.");
        }catch(Exception e){File.WriteAllText(report,"FAIL: "+e);Environment.ExitCode=1;}
        finally{if(!app.HasExited)app.Kill(true);}
    }
    public static void AutomationTest() {
        var report=Path.Combine(Settings.Root,"automation-result.txt");
        Directory.CreateDirectory(Path.Combine(Settings.Root,"exports"));
        var target=Environment.GetEnvironmentVariable("CJ_FINGER_TEST_TARGET") ?? Environment.ProcessPath!;
        using var fixture=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { Arguments="--demo-target",UseShellExecute=false,CreateNoWindow=true })!;
        try {
            Thread.Sleep(2000);
            var settings=new Settings { ProgramPath=target,ExportDirectory=Path.Combine(Settings.Root,"exports"),Username="test-user",PasswordProtected=Settings.Protect("test-password"),LookbackDays=3,DownloadTimeoutSeconds=30 };
            var stages=new List<string>();
            var file=Task.Run(()=>new ExportAutomation(settings,stage=> { stages.Add(stage); File.WriteAllText(Path.Combine(Settings.Root,"test-stage.txt"),stage); },CancellationToken.None).Run()).GetAwaiter().GetResult();
            var today=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"SE Asia Standard Time").Date;
            var content=File.ReadAllText(file);
            if(!content.Contains(today.AddDays(-3).ToString("yyyyMMdd")) || !content.Contains(today.ToString("yyyyMMdd"))) throw new Exception("Date range mismatch");
            if(!stages.SequenceEqual(new[]{"login","select_dates","download","export"})) throw new Exception("Unexpected stage order");
            File.WriteAllText(report,"PASS: UI Automation login, session, 3-day dates, wait ready, TXT export and validation.\n"+file);
        } catch(Exception e) { File.WriteAllText(report,"FAIL: "+e); try { fixture.Refresh(); Native.Screenshot(fixture.MainWindowHandle,Path.Combine(Settings.Root,"fixture-failure.png")); } catch { } Environment.ExitCode=1; }
        finally { if(!fixture.HasExited) fixture.Kill(true); }
    }
    public static void Run() {
        UpdateTests.Run();
        var now=DateTimeOffset.Now;
        foreach(var value in new[]{new Settings(),new Settings {ScheduleStartAt=now.LocalDateTime.AddMinutes(-1)}}) {
            var blocked=false;try {_=TrayApp.FirstRun(value,now);}catch(InvalidOperationException){blocked=true;}
            if(!blocked)throw new Exception("Missing/past start time was accepted.");
        }
        if(TrayApp.FirstRun(new Settings {ScheduleStartAt=now.LocalDateTime.AddMinutes(10)},now)!=now.AddMinutes(10))throw new Exception("Future schedule calculation failed.");
        var directory=Path.Combine(Settings.Root,"self-test"); Directory.CreateDirectory(directory);
        var good=Path.Combine(directory,"good.txt"); File.WriteAllText(good,"BE000609\t20260929\t0500\nBE000609\t20260928\t1641\n");
        ExportAutomation.ValidateFile(good);
        var bad=Path.Combine(directory,"bad.txt"); File.WriteAllText(bad,"BE000609 20260230 2500");
        var rejected=false; try { ExportAutomation.ValidateFile(bad); } catch(InvalidOperationException) { rejected=true; }
        if(!rejected) throw new Exception("Invalid date was accepted.");
        if(Settings.Unprotect(Settings.Protect("self-test-value"))!="self-test-value") throw new Exception("DPAPI round trip failed.");
        File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: future start required, valid TXT, invalid TXT rejection, DPAPI round trip.");
    }
}
