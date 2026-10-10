using System.Diagnostics;
namespace CJFingerService;

internal sealed class ExportReset(int processId,DateTime startedAt) {
    internal static bool IsSavedConfirmation(string title,string text,string exportedFile) => title=="Success" &&
        (text.Contains("\u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e44\u0e1f\u0e25\u0e4c\u0e41\u0e25\u0e49\u0e27") || text.Contains("TXT saved",StringComparison.OrdinalIgnoreCase)) &&
        text.Contains(Path.GetFileName(exportedFile),StringComparison.OrdinalIgnoreCase);
    internal void Complete(string exportedFile,Action<string> report,CancellationToken cancel) {
        Process process;
        try {process=Process.GetProcessById(processId);}catch(ArgumentException){return;}
        using(process) {
            if(process.HasExited)return;
            if(process.StartTime!=startedAt)throw new InvalidOperationException("WEB8 process changed; reset stopped.");
            void Wait(Func<bool> done,string description,int seconds=15) {
                var until=DateTime.UtcNow.AddSeconds(seconds);
                while(DateTime.UtcNow<until) {cancel.ThrowIfCancellationRequested();if(done())return;if(cancel.WaitHandle.WaitOne(200))cancel.ThrowIfCancellationRequested();}
                throw new TimeoutException("WEB8 reset timed out: "+description+". The exported file is already retained.");
            }
            report("detail: X01|Confirming WEB8 TXT saved popup (OK)");
            var popup=Native.FindWindow(processId,"Success");
            if(popup!=IntPtr.Zero) {
                Native.AssertDesktop();
                var controls=Native.ChildControls(popup);
                var text=string.Join("\n",controls.Where(c=>!c.IsPassword).Select(c=>c.Name));
                if(!IsSavedConfirmation(Native.ControlText(popup),text,exportedFile))
                    throw new InvalidOperationException("Unrecognized WEB8 Success dialog; reset stopped. Export retained.");
                var buttons=controls.Where(c=>c.ClassName.Contains("button",StringComparison.OrdinalIgnoreCase) && c.Name.Replace("&","").Trim().Equals("OK",StringComparison.OrdinalIgnoreCase) && c.IsEnabled).ToArray();
                if(buttons.Length!=1 || !Native.PostMessage(buttons[0].Handle,0x00F5,IntPtr.Zero,IntPtr.Zero))throw new InvalidOperationException("Cannot confirm the WEB8 export popup.");
                Wait(()=>Native.FindWindow(processId,"Success")==IntPtr.Zero,"saved popup to close");
            }
            if(process.HasExited)return;
            var main=Native.FindWindow(processId,"Time Access Solution");
            if(main==IntPtr.Zero || !Native.Enabled(main))throw new InvalidOperationException("WEB8 has another dialog open; reset stopped. Close it manually.");
            report("detail: X02|Closing WEB8 to reset the next run");
            Native.AssertDesktop();
            if(!Native.PostMessage(main,0x0010,IntPtr.Zero,IntPtr.Zero))throw new InvalidOperationException("Cannot request WEB8 to close.");
            Wait(()=>process.HasExited,"WEB8 to exit");
            report("detail: X03|WEB8 closed; ready for the next scheduled run");
        }
    }
}
