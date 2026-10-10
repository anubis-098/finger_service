using System.Diagnostics;
using Forms=System.Windows.Forms;
namespace CJFingerService;

internal static class UpdateInstaller {
    // The staged executable owns this window/process, outside the installation folder.
    internal static void Run(string[] args) {
        if(args.Length!=4 || !int.TryParse(args[3],out var parentId))throw new ArgumentException("Invalid updater arguments.");
        var source=AppContext.BaseDirectory;
        var target=Path.GetFullPath(args[1]);
        var ready=Path.GetFullPath(args[2]);
        var log=Path.Combine(Path.GetDirectoryName(source.TrimEnd(Path.DirectorySeparatorChar))!,"install.log");
        using var form=new UpdateProgressForm();
        form.Shown+=async(_,_)=> {
            try {
                form.Report(85,"Waiting for Finger Service to close");
                using var parent=Process.GetProcessById(parentId);
                File.WriteAllText(ready,"ready");
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
                await parent.WaitForExitAsync(timeout.Token);
                var progress=new Progress<int>(p=>form.Report(p,"Installing update"));
                await Task.Run(()=>ReplaceFiles(source,target,progress));
                form.Report(98,"Starting updated Finger Service");
                using var restarted=Process.Start(new ProcessStartInfo(Path.Combine(target,"CJFingerService.exe")) {UseShellExecute=true,WorkingDirectory=target});
                if(restarted is null)throw new InvalidOperationException("Update installed, but restart failed. Start Finger Service manually.");
                File.WriteAllText(log,"Update installed. Backup retained.\n");
                form.Report(100,"Update complete");await Task.Delay(1500);form.Close();
            } catch(Exception e) {
                File.WriteAllText(log,$"Update failed: {e}\n");
                Forms.MessageBox.Show(form,e.Message+"\nDetails: "+log,"Update failed");form.Close();
            }
        };
        Forms.Application.Run(form);
    }

    internal static void ReplaceFiles(string source,string target,IProgress<int>? progress=null) {
        if(string.Equals(Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar),Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Update source and target must differ.");
        var backup=Path.Combine(Path.GetDirectoryName(source.TrimEnd(Path.DirectorySeparatorChar))!,"backup");
        Directory.CreateDirectory(backup);
        foreach(var name in Updater.Files) {
            if(!File.Exists(Path.Combine(source,name)))throw new FileNotFoundException("Missing update file: "+name);
            if(File.Exists(Path.Combine(target,name)))File.Copy(Path.Combine(target,name),Path.Combine(backup,name),false);
        }
        var changed=new List<string>();
        try {
            foreach(var name in Updater.Files) {
                changed.Add(name);
                File.Copy(Path.Combine(source,name),Path.Combine(target,name),true);
                progress?.Report(85+changed.Count*3);
            }
        } catch(Exception failure) {
            var restoreErrors=new List<string>();
            foreach(var name in changed)try {
                var original=Path.Combine(backup,name);var destination=Path.Combine(target,name);
                if(File.Exists(original))File.Copy(original,destination,true);
                else if(File.Exists(destination))File.Delete(destination);
            } catch {restoreErrors.Add(name);}
            throw new IOException($"Installation failed. Rollback {(restoreErrors.Count==0?"completed":"failed for "+string.Join(", ",restoreErrors))}. Backup: {backup}",failure);
        }
    }
}
