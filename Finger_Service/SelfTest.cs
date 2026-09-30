namespace CJFingerService;
internal static class SelfTest {
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
        if(TrayApp.FirstRun(new Settings(),now)!=now || TrayApp.FirstRun(new Settings { ScheduleStartAt=now.LocalDateTime.AddMinutes(-1) },now)!=now || TrayApp.FirstRun(new Settings { ScheduleStartAt=now.LocalDateTime.AddMinutes(10) },now)!=now.AddMinutes(10)) throw new Exception("Schedule start calculation failed.");
        var directory=Path.Combine(Settings.Root,"self-test"); Directory.CreateDirectory(directory);
        var good=Path.Combine(directory,"good.txt"); File.WriteAllText(good,"BE000609\t20260929\t0500\nBE000609\t20260928\t1641\n");
        ExportAutomation.ValidateFile(good);
        var bad=Path.Combine(directory,"bad.txt"); File.WriteAllText(bad,"BE000609 20260230 2500");
        var rejected=false; try { ExportAutomation.ValidateFile(bad); } catch(InvalidOperationException) { rejected=true; }
        if(!rejected) throw new Exception("Invalid date was accepted.");
        if(Settings.Unprotect(Settings.Protect("self-test-value"))!="self-test-value") throw new Exception("DPAPI round trip failed.");
        File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: valid TXT, invalid TXT rejection, DPAPI round trip.");
    }
}
