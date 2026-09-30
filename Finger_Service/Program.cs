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
        if(args.Contains("--self-test")) { SelfTest.Run(); return; }
        if(args.Contains("--startup-test")) { SelfTest.StartupTest(); Environment.Exit(Environment.ExitCode); return; }
        if(args.Contains("--start-test")) { SelfTest.StartupTest(true); Environment.Exit(Environment.ExitCode); return; }
        if(args.Contains("--demo-target")) { Forms.Application.Run(new DemoTarget()); return; }
        if(args.Contains("--automation-test")) { SelfTest.AutomationTest(); return; }
        if(args.Contains("--set-password")) { var settings=Settings.Load(); settings.PasswordProtected=Settings.Protect(Console.In.ReadToEnd().TrimEnd('\r','\n')); settings.Save(); return; }
        // UI Automation providers may leave COM threads alive after completion.
        // This isolated worker owns no UI; exit after its result has been flushed.
        if(args.Contains("--worker")) { RunWorker(); Environment.Exit(0); return; }
        if(args.Contains("--upload-only")) { RunWorker(true); Environment.Exit(0); return; }
        using var mutex=new Mutex(true,"Local\\CJFingerService-Tray",out var first);
        if(!first) return;
        Forms.Application.Run(new TrayApp());
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
            var export=Task.Run(()=>new ExportAutomation(settings,stage=>File.WriteAllText(Path.Combine(Settings.Root,"worker-stage.txt"),stage),CancellationToken.None).Run()).GetAwaiter().GetResult();
            var run=Guid.NewGuid().ToString("N");
            var directory=Path.Combine(Settings.Root,"pending",run); Directory.CreateDirectory(directory);
            var copy=Path.Combine(directory,Path.GetFileName(export)); File.Copy(export,copy,false);
            ExportAutomation.ValidateFile(copy);
            File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonSerializer.Serialize(new { runId=run,filename=Path.GetFileName(export),source=export,createdAt=DateTimeOffset.Now,status="pending_upload",sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(copy))) }));
            var message="Export saved locally. Server upload disabled.";
            if(settings.EnableUpload) {
                File.WriteAllText(Path.Combine(Settings.Root,"worker-stage.txt"),"upload");
                var sent=UploadClient.SendPending(settings).GetAwaiter().GetResult();
                message=$"Export saved; uploaded {sent} queued file(s) to server.";
            }
            File.WriteAllText(result,JsonSerializer.Serialize(new { ok=true,file=copy,message }));
        } catch(Exception e) {
            // Never include UI dumps, passwords, tokens, or screenshots in logs.
            File.WriteAllText(Path.Combine(Settings.Root,"worker-error.log"),$"{DateTimeOffset.Now:O}\n{e.GetType().FullName} HRESULT=0x{e.HResult:X8}\n{e.StackTrace}");
            File.WriteAllText(result,JsonSerializer.Serialize(new { ok=false,file="",message=e.Message }));
        }
    }
}
