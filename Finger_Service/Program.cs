using System.Diagnostics;
using System.Text.Json;
using Forms = System.Windows.Forms;

namespace CJFingerService;
internal static class Program {
    [STAThread]
    static void Main(string[] args) {
        Directory.CreateDirectory(Settings.Root);
        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
        Forms.Application.EnableVisualStyles();
        Forms.Application.SetCompatibleTextRenderingDefault(false);
        if(args.Contains("--install-update")) { UpdateInstaller.Run(args); return; }
        if(args.Contains("--self-test")) { SelfTest.Run(); return; }
        if(args.Contains("--status-test")) { SelfTest.StatusTest(); return; }
        if(args.Contains("--startup-test")) { SelfTest.StartupTest(); Environment.Exit(Environment.ExitCode); return; }
        if(args.Contains("--start-test")) { SelfTest.StartupTest(true); Environment.Exit(Environment.ExitCode); return; }
        if(args.Contains("--cancel-test")) { SelfTest.StartupTest(true,"Cancel"); Environment.Exit(Environment.ExitCode); return; }
        if(args.Contains("--exit-test")) { SelfTest.StartupTest(true,"Exit"); Environment.Exit(Environment.ExitCode); return; }
        if(args.Contains("--demo-target")) { Forms.Application.Run(new DemoTarget()); return; }
        if(args.Contains("--automation-test")) { SelfTest.AutomationTest(); Environment.Exit(Environment.ExitCode); return; }
        if(args.Contains("--set-password")) { var settings=Settings.Load(); settings.PasswordProtected=Settings.Protect(Console.In.ReadToEnd().TrimEnd('\r','\n')); settings.Save(); return; }
        // UI Automation providers may leave COM threads alive after completion.
        // This isolated worker owns no UI; exit after its result has been flushed.
        if(args.Contains("--worker")) { RunWorker(); Environment.Exit(0); return; }
        if(args.Contains("--upload-only")) { RunWorker(true); Environment.Exit(0); return; }
        var mutexName="Local\\CJFingerService-Tray";
        if(args.Contains("--isolated-test-tray")) {
            if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CJ_FINGER_SERVICE_DATA")))throw new InvalidOperationException("An isolated test data directory is required.");
            mutexName+="-test-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Settings.Root)))[..16];
        }
        using var mutex=new Mutex(true,mutexName,out var first);
        if(!first) return;
        Forms.Application.Run(new TrayApp());
    }
    static void WriteStage(string stage) {
        // Progress reporting must never abort an export when a reader holds the file.
        for(var attempt=0;attempt<5;attempt++) {
            try { File.WriteAllText(Path.Combine(Settings.Root,"worker-stage.txt"),stage); return; }
            catch(IOException) { Thread.Sleep(50); }
        }
    }
    static void RunWorker(bool uploadOnly=false) {
        var result=Path.Combine(Settings.Root,"worker-result.json");
        try {
            var settings=Settings.Load();
            if(uploadOnly) {
                if(!settings.EnableUpload) throw new InvalidOperationException("Enable server uploads in Settings first.");
                var sent=UploadClient.SendPending(settings).GetAwaiter().GetResult();
                File.WriteAllText(result,JsonSerializer.Serialize(new {ok=true,file="",message=$"Uploaded {sent} queued file(s)."})); return;
            }
            var automation=new ExportAutomation(settings,WriteStage,CancellationToken.None);
            var export=Task.Run(()=>automation.Run()).GetAwaiter().GetResult();
            WriteStage("detail: Q01|Saving validated TXT to the local upload queue");
            var run=Guid.NewGuid().ToString("N");
            var directory=Path.Combine(Settings.Root,"pending",run); Directory.CreateDirectory(directory);
            var copy=Path.Combine(directory,Path.GetFileName(export)); File.Copy(export,copy,false);
            ExportAutomation.ValidateFile(copy);
            File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonSerializer.Serialize(new { runId=run,filename=Path.GetFileName(export),source=export,createdAt=DateTimeOffset.Now,status="pending_upload",sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(copy))) }));
            var message="Export saved locally. Server upload disabled.";
            if(settings.EnableUpload) {
                WriteStage("upload");
                var sent=UploadClient.SendPending(settings).GetAwaiter().GetResult();
                message=$"Export saved; uploaded {sent} queued file(s) to server.";
            }
            try {Task.Run(()=>automation.Complete(export)).GetAwaiter().GetResult();}
            catch(Exception e) {throw new InvalidOperationException(message+" WEB8 reset failed: "+e.Message,e);}
            message+=" WEB8 closed for the next run.";
            File.WriteAllText(result,JsonSerializer.Serialize(new { ok=true,file=copy,message }));
        } catch(Exception e) {
            // Never include UI dumps, passwords, tokens, or screenshots in logs.
            File.WriteAllText(Path.Combine(Settings.Root,"worker-error.log"),$"{DateTimeOffset.Now:O}\n{e.GetType().FullName} HRESULT=0x{e.HResult:X8}\n{e.StackTrace}");
            File.WriteAllText(result,JsonSerializer.Serialize(new { ok=false,file="",message=e.Message }));
        }
    }
}
