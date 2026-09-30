using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using Forms = System.Windows.Forms;

namespace CJFingerService;
internal sealed class ExportAutomation(Settings settings, Action<string> stage, CancellationToken cancel) {
    private int processId;
    private void Delay(int ms) { if (cancel.WaitHandle.WaitOne(ms)) cancel.ThrowIfCancellationRequested(); }
    private static string Normalize(string value) => Regex.Replace(value, @"\s+", "").ToLowerInvariant();
    private AutomationElement? Window(string title) {
        var handle=Native.FindWindow(processId,title);
        if(handle==IntPtr.Zero) return null;
        try { return AutomationElement.FromHandle(handle); }
        catch(ElementNotAvailableException) { return null; } // Window closed between enumeration and UIA lookup; retry in Wait.
    }
    private T Wait<T>(Func<T?> action, string description, int seconds=30) where T:class {
        var until=DateTime.UtcNow.AddSeconds(seconds);
        while(DateTime.UtcNow < until) { cancel.ThrowIfCancellationRequested(); Native.AssertDesktop(); var result=action(); if(result != null) return result; Delay(500); }
        throw new TimeoutException("Timed out: " + description);
    }
    private void Front(AutomationElement window) {
        Native.AssertDesktop(); var handle=(IntPtr)window.Current.NativeWindowHandle;
        Native.ShowWindow(handle,9); Native.SetForegroundWindow(handle); Delay(350);
        if(Native.GetForegroundWindow()!=handle) throw new InvalidOperationException("FOCUS_LOST: cannot activate the expected export window.");
    }
    private AutomationElement[] Controls(AutomationElement window, ControlType type) => window.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,type)).Cast<AutomationElement>().Where(e=>!e.Current.IsOffscreen).ToArray();
    private static string Text(AutomationElement window) => string.Join("\n",window.FindAll(TreeScope.Descendants,Condition.TrueCondition).Cast<AutomationElement>().Select(e=> {
        try { if(e.Current.IsPassword) return ""; if(e.TryGetCurrentPattern(ValuePattern.Pattern,out var p)) return ((ValuePattern)p).Current.Value; if(e.TryGetCurrentPattern(TextPattern.Pattern,out var text)) return ((TextPattern)text).DocumentRange.GetText(-1); return e.Current.Name; } catch { return ""; }
    }));
    private sealed record OcrLine(string text,double x,double y);
    private OcrLine[] ReadOcr(AutomationElement window) {
        Front(window); var image=Path.Combine(Settings.Root,"ocr-current.png");
        Native.Screenshot((IntPtr)window.Current.NativeWindowHandle,image);
        try {
            var start=new ProcessStartInfo("powershell.exe") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=System.Text.Encoding.UTF8 };
            foreach(var argument in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",Path.Combine(AppContext.BaseDirectory,"ocr.ps1"),"-ImagePath",image,"-Language",settings.OcrLanguage}) start.ArgumentList.Add(argument);
            using var p=Process.Start(start)!; var output=p.StandardOutput.ReadToEndAsync(); var errors=p.StandardError.ReadToEndAsync();
            if(!p.WaitForExit(20000)) { p.Kill(true); throw new TimeoutException("OCR timeout."); }
            if(p.ExitCode!=0) throw new InvalidOperationException("OCR unavailable. Install Thai OCR in Windows language settings; use UI Automation diagnostics.");
            return JsonSerializer.Deserialize<OcrLine[]>(output.GetAwaiter().GetResult()) ?? [];
        } finally { if(File.Exists(image)) File.Delete(image); }
    }
    private void Click(AutomationElement window,string caption) {
        cancel.ThrowIfCancellationRequested(); Front(window);
        var button=Controls(window,ControlType.Button).FirstOrDefault(e=>Normalize(e.Current.Name)==Normalize(caption));
        if(button!=null) {
            if(!button.Current.IsEnabled) throw new InvalidOperationException("Button is disabled: " + caption);
            if(button.Current.NativeWindowHandle!=0 && button.Current.ClassName.Contains("button",StringComparison.OrdinalIgnoreCase)) {
                if(!Native.PostMessage((IntPtr)button.Current.NativeWindowHandle,0x00F5,IntPtr.Zero,IntPtr.Zero)) throw new InvalidOperationException("Cannot click native button: " + caption);
                return;
            }
            // Invoke on a separate MTA thread: modal dialogs may block the
            // provider until they close. Each following step verifies its UI state.
            if(button.TryGetCurrentPattern(InvokePattern.Pattern,out var pattern)) {
                var invocation=Task.Run(()=>((InvokePattern)pattern).Invoke());
                Delay(250);
                if(invocation.IsFaulted) throw new InvalidOperationException("Control invocation failed: " + caption,invocation.Exception);
                _=invocation.ContinueWith(task=> { _=task.Exception; },TaskContinuationOptions.OnlyOnFaulted);
                return;
            }
            var bounds=button.Current.BoundingRectangle; Native.Click((IntPtr)window.Current.NativeWindowHandle,bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2); return;
        }
        // Exact OCR caption only: never click the centre of a line containing several controls.
        var matches=ReadOcr(window).Where(line=>Normalize(line.text)==Normalize(caption)).ToArray();
        if(matches.Length!=1) throw new InvalidOperationException("Cannot uniquely locate button: " + caption);
        Native.GetWindowRect((IntPtr)window.Current.NativeWindowHandle,out var rect);
        Native.Click((IntPtr)window.Current.NativeWindowHandle,rect.Left+matches[0].x,rect.Top+matches[0].y);
    }
    private static void SetValue(AutomationElement control,string value) {
        if(!control.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern) || ((ValuePattern)pattern).Current.IsReadOnly) throw new InvalidOperationException("Input does not expose an editable ValuePattern; inspect controls before running.");
        ((ValuePattern)pattern).SetValue(value);
        if(!control.Current.IsPassword && ((ValuePattern)pattern).Current.Value!=value) throw new InvalidOperationException("Input verification failed.");
    }
    private void FillLogin(AutomationElement window,AutomationElement control,string value,string field) {
        Front(window);
        control.SetFocus(); Delay(200);
        void VerifyFocus() {
            if(Native.GetForegroundWindow()!=(IntPtr)window.Current.NativeWindowHandle || !control.Current.HasKeyboardFocus)
                throw new InvalidOperationException($"FOCUS_LOST: {field} input is not focused. No further credentials were typed.");
        }
        // Keyboard events work with embedded web login forms; ValuePattern can
        // display text without updating their internal form state. Never read back a password.
        Native.ReplaceFocusedText(value,VerifyFocus);
        Delay(350);
        if(field=="Username" && control.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern)) {
            for(var attempt=0;attempt<15;attempt++) {
                if(((ValuePattern)pattern).Current.Value==value) return;
                Delay(100);
            }
            // Some web providers report an empty/stale value. The login success
            // message below is the authoritative verification, not that readback.
            File.AppendAllText(Path.Combine(Settings.Root,"service.log"),$"{DateTimeOffset.Now:O} Username readback unavailable after keyboard entry; checking login result instead.\n");
        }
    }
    private static void SetDate(AutomationElement control,DateTime date) {
        if(!control.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern) || ((ValuePattern)pattern).Current.IsReadOnly) throw new InvalidOperationException("Date picker does not expose an editable ValuePattern in this program version.");
        var value=(ValuePattern)pattern;
        control.SetFocus();
        Native.EnterDate((IntPtr)control.Current.NativeWindowHandle,date);
        string actual="";
        for(var attempt=0;attempt<20;attempt++) {
            actual=value.Current.Value;
            if(actual==date.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture) || (DateTime.TryParse(actual,CultureInfo.CurrentCulture,DateTimeStyles.None,out var parsed)&&parsed.Date==date.Date)) return;
            Thread.Sleep(100);
        }
        throw new InvalidOperationException($"Date picker verification failed: received '{actual}', expected {date:yyyy-MM-dd}; culture {CultureInfo.CurrentCulture.Name}.");
    }
    public string Run() {
        settings.Validate(false); Native.AssertDesktop();
        var processName=Path.GetFileNameWithoutExtension(settings.ProgramPath);
        var candidates=Process.GetProcessesByName(processName).Where(p=> { try { return string.Equals(p.MainModule?.FileName,settings.ProgramPath,StringComparison.OrdinalIgnoreCase) && p.MainWindowTitle.Contains("Time Access Solution"); } catch { return false; } }).ToArray();
        if(candidates.Length>1) throw new InvalidOperationException("Several export windows are running. Keep one instance only.");
        using var process=candidates.FirstOrDefault() ?? Process.Start(new ProcessStartInfo(settings.ProgramPath) { UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(settings.ProgramPath)!,WindowStyle=ProcessWindowStyle.Normal })!;
        processId=process.Id;
        var main=Wait(()=>Window("Time Access Solution"),"export main window");
        stage("login");
        var login=Window("Login Session");
        if(login==null) { Click(main,"1. \u0e25\u0e47\u0e2d\u0e01\u0e2d\u0e34\u0e19\u0e40\u0e02\u0e49\u0e32\u0e23\u0e30\u0e1a\u0e1a"); login=Wait(()=>Window("Login Session"),"login window"); }
        Front(login);
        Wait(()=>Controls(login,ControlType.Edit).Length>=2 || Text(login).Contains("\u0e23\u0e30\u0e1a\u0e1a\u0e25\u0e47\u0e2d\u0e01\u0e2d\u0e34\u0e19\u0e40\u0e23\u0e35\u0e22\u0e1a\u0e23\u0e49\u0e2d\u0e22\u0e41\u0e25\u0e49\u0e27") ? login:null,"login form or existing session");
        var edits=Controls(login,ControlType.Edit).OrderBy(e=>e.Current.BoundingRectangle.Top).ToArray();
        if(edits.Length>=2) {
            var password=edits.FirstOrDefault(e=>e.Current.IsPassword)
                ?? edits.FirstOrDefault(e=>e.Current.Name.Contains("password",StringComparison.OrdinalIgnoreCase))
                ?? (edits.Length==2?edits[1]:throw new InvalidOperationException("Cannot uniquely identify Password input."));
            var usernames=edits.Where(e=>!Automation.Compare(e,password)).ToArray();
            var username=usernames.FirstOrDefault(e=>e.Current.Name.Contains("username",StringComparison.OrdinalIgnoreCase))
                ?? (usernames.Length==1?usernames[0]:throw new InvalidOperationException("Cannot uniquely identify Username input."));
            FillLogin(login,username,settings.Username,"Username");
            FillLogin(login,password,Settings.Unprotect(settings.PasswordProtected),"Password"); Click(login,"Log in");
        }
        Wait(()=> {
            var text=Text(login);
            if(text.Contains("\u0e23\u0e30\u0e1a\u0e1a\u0e25\u0e47\u0e2d\u0e01\u0e2d\u0e34\u0e19\u0e40\u0e23\u0e35\u0e22\u0e1a\u0e23\u0e49\u0e2d\u0e22\u0e41\u0e25\u0e49\u0e27") || text.Contains("\u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e2a\u0e33\u0e40\u0e23\u0e47\u0e08")) return login;
            return null;
        },"successful login",45);
        Click(login,"\u0e43\u0e0a\u0e49 Session \u0e19\u0e35\u0e49");
        DateTime? readySince=null;
        Wait(()=> {
            var popup=Window("Success");
            if(popup is not null) {
                if(popup.Current.Name!="Success" || !Normalize(Text(popup)).Contains(Normalize("\u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01 Session \u0e41\u0e25\u0e49\u0e27 \u0e41\u0e25\u0e30\u0e42\u0e2b\u0e25\u0e14 Area \u0e40\u0e23\u0e35\u0e22\u0e1a\u0e23\u0e49\u0e2d\u0e22")))
                    throw new InvalidOperationException("Unexpected Success dialog after session. Please inspect it manually.");
                Click(popup,"OK");
                Wait(()=>Native.FindWindow(processId,"Success")==IntPtr.Zero?main:null,"session confirmation closes",10);
                readySince=null;
            }
            if(Native.FindWindow(processId,"Login Session")!=IntPtr.Zero || !main.Current.IsEnabled) { readySince=null;return null; }
            readySince ??=DateTime.UtcNow;
            return DateTime.UtcNow-readySince>=TimeSpan.FromSeconds(2)?main:null;
        },"session closes and Success / OK confirmation",45);
        Front(main);
        stage("select_dates");
        var dates=main.FindAll(TreeScope.Descendants,Condition.TrueCondition).Cast<AutomationElement>().Where(e=>e.Current.ClassName.Contains("SysDateTimePick32") && e.Current.NativeWindowHandle!=0).OrderBy(e=>e.Current.BoundingRectangle.Left).ToArray();
        if(dates.Length!=2) throw new InvalidOperationException("Expected two native date pickers. Run Diagnostics to inspect this program version.");
        var today=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"SE Asia Standard Time").Date;
        SetDate(dates[0],today.AddDays(-settings.LookbackDays));
        SetDate(dates[1],today);
        var folder=Controls(main,ControlType.Edit).OrderByDescending(e=>e.Current.BoundingRectangle.Top).FirstOrDefault();
        if(folder==null || folder.Current.BoundingRectangle.Top < main.Current.BoundingRectangle.Top+main.Current.BoundingRectangle.Height*.7) throw new InvalidOperationException("Cannot locate export directory input.");
        if(!folder.TryGetCurrentPattern(ValuePattern.Pattern,out var folderPattern) || !string.Equals(((ValuePattern)folderPattern).Current.Value,settings.ExportDirectory,StringComparison.OrdinalIgnoreCase)) {
            SetValue(folder,settings.ExportDirectory); Click(main,"\u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e04\u0e48\u0e32");
        }
        stage("download");
        var previous=Text(main);
        Click(main,"2. \u0e14\u0e36\u0e07\u0e02\u0e49\u0e2d\u0e21\u0e39\u0e25");
        var changed=false;
        Wait(()=> {
            var text=Text(main); if(text!=previous) changed=true;
            var download=Controls(main,ControlType.Button).FirstOrDefault(e=>Normalize(e.Current.Name)==Normalize("2. \u0e14\u0e36\u0e07\u0e02\u0e49\u0e2d\u0e21\u0e39\u0e25"));
            if(download is not null && !download.Current.IsEnabled) changed=true;
            var save=Controls(main,ControlType.Button).FirstOrDefault(e=>Normalize(e.Current.Name)==Normalize("3. \u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e44\u0e1f\u0e25\u0e4c txt"));
            return changed && text.Contains("status: ready",StringComparison.OrdinalIgnoreCase) && (save==null || save.Current.IsEnabled) ? main:null;
        },"fresh download status: ready",settings.DownloadTimeoutSeconds);
        stage("export");
        var before=Directory.GetFiles(settings.ExportDirectory,"*.txt").ToDictionary(p=>p,p=>new FileInfo(p).LastWriteTimeUtc);
        var exportStart=DateTime.UtcNow; Click(main,"3. \u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e44\u0e1f\u0e25\u0e4c txt");
        string? stablePath=null; long stableLength=-1; DateTime stableSince=DateTime.UtcNow;
        var file=Wait(()=> {
            var files=Directory.GetFiles(settings.ExportDirectory,"*.txt").Select(p=>new FileInfo(p)).Where(f=>f.LastWriteTimeUtc>=exportStart.AddSeconds(-1) && (!before.TryGetValue(f.FullName,out var old)||old!=f.LastWriteTimeUtc)).ToArray();
            if(files.Length>1) throw new InvalidOperationException("Several TXT files changed; cannot identify the export safely.");
            if(files.Length==0) return null;
            var candidate=files[0];
            if(candidate.FullName!=stablePath || candidate.Length!=stableLength) { stablePath=candidate.FullName; stableLength=candidate.Length; stableSince=DateTime.UtcNow; return null; }
            if(candidate.Length==0 || DateTime.UtcNow-stableSince<TimeSpan.FromSeconds(3)) return null;
            try { using var stream=File.Open(candidate.FullName,FileMode.Open,FileAccess.Read,FileShare.None); return candidate.FullName; } catch(IOException) { return null; }
        },"new, stable TXT export",90);
        ValidateFile(file); return file;
    }
    internal static void ValidateFile(string file) {
        var info=new FileInfo(file); if(info.Length==0 || info.Length>10*1024*1024) throw new InvalidOperationException("TXT must contain data and be at most 10 MB.");
        var count=0;
        foreach(var line in File.ReadLines(file)) {
            if(string.IsNullOrWhiteSpace(line)) continue;
            var cells=Regex.Split(line.Trim().TrimStart('\uFEFF'),@"\s+");
            if(cells.Length!=3 || !Regex.IsMatch(cells[1],@"^\d{8}$") || !Regex.IsMatch(cells[2],@"^\d{4}$") || !DateTime.TryParseExact(cells[1]+cells[2],"yyyyMMddHHmm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)) throw new InvalidOperationException("Invalid fingerprint row " + (count+1));
            count++;
        }
        if(count==0) throw new InvalidOperationException("No fingerprint rows in TXT.");
    }
}
