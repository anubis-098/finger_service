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
    private T Wait<T>(Func<T?> action, string description, int seconds=30, Action? retry=null) where T:class {
        var attempts=retry is null?1:3;
        for(var attempt=1;attempt<=attempts;attempt++) {
            var until=DateTime.UtcNow.AddSeconds(seconds);
            while(DateTime.UtcNow < until) {
                cancel.ThrowIfCancellationRequested(); Native.AssertDesktop();
                try {var result=action();if(result is not null)return result;}catch(ElementNotAvailableException){/* Re-query controls while the target redraws. */}
                Delay(500);
            }
            if(attempt<attempts) { Report($"Retry {attempt+1}/{attempts}: {description}");retry!(); }
        }
        throw new TimeoutException($"Timed out after {attempts} attempt(s): {description}");
    }
    private void Report(string message) {
        stage(message);
        File.AppendAllText(Path.Combine(Settings.Root,"service.log"),$"{DateTimeOffset.Now:O} STAGE {message}\n");
    }
    private void Front(AutomationElement window) {
        Native.AssertDesktop(); var handle=(IntPtr)window.Current.NativeWindowHandle;
        for(var attempt=0;attempt<3;attempt++) {
            Native.ShowWindow(handle,9); Native.SetForegroundWindow(handle); Delay(350);
            if(Native.GetForegroundWindow()==handle)return;
            Delay(500);
        }
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
            if(settings.UseUbuntuOcr) {
                using var client=new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler {UseProxy=false}) {Timeout=TimeSpan.FromSeconds(25)};
                using var request=new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post,"http://127.0.0.1:17863/ocr");
                request.Headers.Add("X-OCR-Token",settings.OcrBridgeToken);
                request.Content=new System.Net.Http.ByteArrayContent(File.ReadAllBytes(image));
                request.Content.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                try {
                    using var response=client.SendAsync(request,cancel).GetAwaiter().GetResult();
                    if(!response.IsSuccessStatusCode) throw new InvalidOperationException($"Ubuntu OCR returned {(int)response.StatusCode}. Check the bridge token and Tesseract installation.");
                    var json=response.Content.ReadAsStringAsync(cancel).GetAwaiter().GetResult();
                    return JsonSerializer.Deserialize<OcrLine[]>(json) ?? [];
                } catch(System.Net.Http.HttpRequestException e) {throw new InvalidOperationException("Ubuntu OCR bridge is unavailable. Start ubuntu/ocr_bridge.py on the same computer.",e);}
            }
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
    private void SetValue(AutomationElement control,string value) {
        if(!control.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern) || ((ValuePattern)pattern).Current.IsReadOnly) throw new InvalidOperationException("Input does not expose an editable ValuePattern; inspect controls before running.");
        ((ValuePattern)pattern).SetValue(value);
        if(!control.Current.IsPassword) {
            for(var attempt=0;attempt<20;attempt++) {if(((ValuePattern)pattern).Current.Value==value)return;Delay(100);}
            throw new InvalidOperationException("Export directory input verification failed.");
        }
    }
    private sealed class LoginRetryException(string message) : Exception(message);
    private (AutomationElement Username,AutomationElement Password) LoginFields(AutomationElement window) {
        var edits=Controls(window,ControlType.Edit).Where(e=>e.Current.IsEnabled).ToArray();
        string Label(AutomationElement e)=>Normalize(e.Current.Name+" "+(e.Current.LabeledBy?.Current.Name ?? ""));
        var passwords=edits.Where(e=>e.Current.IsPassword || Label(e).Contains("password")).ToArray();
        if(passwords.Length!=1)throw new LoginRetryException("Password selector is missing or ambiguous.");
        var password=passwords[0];
        var candidates=edits.Where(e=>!e.Current.IsPassword && !Automation.Compare(e,password)).ToArray();
        var named=candidates.Where(e=>Label(e).Contains("username")).ToArray();
        var username=named.Length==1?named[0]:named.Length==0 && candidates.Length==1?candidates[0]:null;
        if(username is null)throw new LoginRetryException("Username selector is missing or ambiguous.");
        if(username.Current.BoundingRectangle.IntersectsWith(password.Current.BoundingRectangle))
            throw new LoginRetryException("Login inputs overlap; waiting for the form to settle.");
        return (username,password);
    }
    private void VerifyUsername(AutomationElement control,string expected) {
        for(var attempt=0;attempt<15;attempt++) {
            cancel.ThrowIfCancellationRequested();
            if(control.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern) && ((ValuePattern)pattern).Current.Value==expected)return;
            if(control.TryGetCurrentPattern(TextPattern.Pattern,out var text) && ((TextPattern)text).DocumentRange.GetText(-1).TrimEnd('\r','\n')==expected)return;
            Delay(100);
        }
        throw new LoginRetryException("Username verification failed; credentials were not submitted.");
    }
    private void FillLogin(AutomationElement window,string value,string field) {
        var fields=LoginFields(window);
        var control=field=="Username"?fields.Username:fields.Password;
        void VerifyFocus() {
            cancel.ThrowIfCancellationRequested(); Native.AssertDesktop();
            var focused=AutomationElement.FocusedElement;
            if(Native.GetForegroundWindow()!=(IntPtr)window.Current.NativeWindowHandle || !control.Current.HasKeyboardFocus || focused is null || !Automation.Compare(control,focused))
                throw new LoginRetryException($"FOCUS_LOST: {field} input is not focused. Typing stopped.");
        }
        var focusedCorrectly=false;
        for(var attempt=0;attempt<3 && !focusedCorrectly;attempt++) {
            Front(window);
            try {
                try { control.SetFocus(); }
                catch(InvalidOperationException) { throw new LoginRetryException("Login field could not receive focus."); }
                Delay(150);
                var bounds=control.Current.BoundingRectangle;
                if(bounds.IsEmpty || bounds.Width<8 || bounds.Height<8)throw new LoginRetryException("Login field is not ready.");
                Native.Click((IntPtr)window.Current.NativeWindowHandle,bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2);
                for(var check=0;check<4;check++){Delay(100);VerifyFocus();}
                focusedCorrectly=true;
            } catch(LoginRetryException) { if(attempt==2)throw; Delay(300); }
        }
        // Keyboard events work with embedded web login forms; ValuePattern can
        // display text without updating their internal form state. Never read back a password.
        Native.ReplaceFocusedText(value,VerifyFocus);
        Delay(350);
        VerifyFocus();
        if(field=="Username")VerifyUsername(control,value);
        else if(Native.EditTextLength((IntPtr)control.Current.NativeWindowHandle) is int length && length!=value.Length)
            throw new LoginRetryException("Password entry length verification failed; credentials were not submitted.");
    }
    private bool LoginSucceeded(AutomationElement window) {
        var text=Text(window);
        return (text.Contains("\u0e23\u0e30\u0e1a\u0e1a\u0e25\u0e47\u0e2d\u0e01\u0e2d\u0e34\u0e19\u0e40\u0e23\u0e35\u0e22\u0e1a\u0e23\u0e49\u0e2d\u0e22\u0e41\u0e25\u0e49\u0e27") || text.Contains("\u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e2a\u0e33\u0e40\u0e23\u0e47\u0e08")) &&
            Controls(window,ControlType.Button).Any(e=>Normalize(e.Current.Name)==Normalize("\u0e43\u0e0a\u0e49 Session \u0e19\u0e35\u0e49") && e.Current.IsEnabled);
    }
    private static bool LoginRejected(string text) =>
        Regex.IsMatch(text,@"login failed|invalid (username|password|credentials)|incorrect (username|password)|authentication failed",RegexOptions.IgnoreCase) ||
        text.Contains("\u0e23\u0e2b\u0e31\u0e2a\u0e1c\u0e48\u0e32\u0e19\u0e44\u0e21\u0e48\u0e16\u0e39\u0e01\u0e15\u0e49\u0e2d\u0e07") ||
        text.Contains("\u0e40\u0e02\u0e49\u0e32\u0e2a\u0e39\u0e48\u0e23\u0e30\u0e1a\u0e1a\u0e44\u0e21\u0e48\u0e2a\u0e33\u0e40\u0e23\u0e47\u0e08");
    private AutomationElement Authenticate() {
        for(var attempt=1;attempt<=3;attempt++) {
            var login=Window("Login Session") ?? throw new InvalidOperationException("Login window closed unexpectedly.");
            try {
                if(LoginSucceeded(login))return login;
                if(attempt>1)Report($"Retry {attempt}/3: login; reacquiring both fields");
                Wait(()=>Controls(login,ControlType.Edit).Count(e=>e.Current.IsEnabled)>=2?login:null,"editable login form",10);
                FillLogin(login,settings.Username,"Username");
                FillLogin(login,Settings.Unprotect(settings.PasswordProtected),"Password");
                VerifyUsername(LoginFields(login).Username,settings.Username);
                Click(login,"Log in");
                var submittedAt=DateTime.UtcNow;
                Wait(()=> {
                    if(LoginSucceeded(login))return login;
                    foreach(var caption in new[]{"Error","Login failed","Authentication failed"}) {
                        var popup=Window(caption);
                        if(popup is not null && LoginRejected(Text(popup)) && Controls(popup,ControlType.Button).Any(e=>Normalize(e.Current.Name)=="ok" && e.Current.IsEnabled)) {
                            Click(popup,"OK");
                            throw new LoginRetryException("Login was rejected by a recognized dialog.");
                        }
                    }
                    var text=Text(login);
                    if(DateTime.UtcNow-submittedAt>TimeSpan.FromSeconds(2) && LoginRejected(text) &&
                        Controls(login,ControlType.Button).Any(e=>Normalize(e.Current.Name)=="login" && e.Current.IsEnabled))
                        throw new LoginRetryException("Login was rejected.");
                    return null;
                },"successful login",45);
                return login;
            } catch(Exception e) when(e is LoginRetryException or ElementNotAvailableException or TimeoutException || e is InvalidOperationException && e.Message.StartsWith("FOCUS_LOST:")) {
                Report($"Retry check {attempt}/3: login incomplete ({e.GetType().Name}); no session selected");
                if(attempt==3)throw new InvalidOperationException("LOGIN_FAILED: stopped after 3 attempts. Check credentials, login fields and the target window. No session was selected.");
                Delay(1500);
            }
        }
        throw new InvalidOperationException("LOGIN_FAILED");
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
        if(settings.UseUbuntuOcr || Native.IsWine) return new WineExportAutomation(settings,stage,cancel).Run();
        settings.Validate(false); Native.AssertDesktop();
        var processName=Path.GetFileNameWithoutExtension(settings.ProgramPath);
        var candidates=Process.GetProcessesByName(processName).Where(p=> { try { return string.Equals(p.MainModule?.FileName,settings.ProgramPath,StringComparison.OrdinalIgnoreCase) && p.MainWindowTitle.Contains("Time Access Solution"); } catch { return false; } }).ToArray();
        if(candidates.Length>1) throw new InvalidOperationException("Several export windows are running. Keep one instance only.");
        using var process=candidates.FirstOrDefault() ?? Process.Start(new ProcessStartInfo(settings.ProgramPath) { UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(settings.ProgramPath)!,WindowStyle=ProcessWindowStyle.Normal })!;
        processId=process.Id;
        var main=Wait(()=>Window("Time Access Solution"),"export main window");
        Report("login");
        var login=Window("Login Session");
        if(login==null) {
            // Use the exact target caption when retrying a lost click.
            void ClickLogin() {if(main.Current.IsEnabled)Click(main,"1. \u0e25\u0e47\u0e2d\u0e01\u0e2d\u0e34\u0e19\u0e40\u0e02\u0e49\u0e32\u0e23\u0e30\u0e1a\u0e1a");}
            ClickLogin();login=Wait(()=>Window("Login Session"),"login window",10,ClickLogin);
        }
        login=Authenticate();
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
        Report("select_dates");
        var dates=main.FindAll(TreeScope.Descendants,Condition.TrueCondition).Cast<AutomationElement>().Where(e=>e.Current.ClassName.Contains("SysDateTimePick32") && e.Current.NativeWindowHandle!=0).OrderBy(e=>e.Current.BoundingRectangle.Left).ToArray();
        if(dates.Length!=2) throw new InvalidOperationException("Expected two native date pickers. Run Diagnostics to inspect this program version.");
        var today=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"SE Asia Standard Time").Date;
        SetDate(dates[0],today.AddDays(-settings.LookbackDays));
        SetDate(dates[1],today);
        var folder=Controls(main,ControlType.Edit).OrderByDescending(e=>e.Current.BoundingRectangle.Top).FirstOrDefault();
        if(folder==null || folder.Current.BoundingRectangle.Top < main.Current.BoundingRectangle.Top+main.Current.BoundingRectangle.Height*.7) throw new InvalidOperationException("Cannot locate export directory input.");
        var directoryChanged=!folder.TryGetCurrentPattern(ValuePattern.Pattern,out var folderPattern) || !string.Equals(((ValuePattern)folderPattern).Current.Value,settings.ExportDirectory,StringComparison.OrdinalIgnoreCase);
        if(directoryChanged) {
            SetValue(folder,settings.ExportDirectory);
        }
        void SaveFolder() { Click(main,"\u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e04\u0e48\u0e32");Delay(500); }
        void SaveDirectory() {
        SaveFolder();
        Wait(()=> {
            var success=Window("Success");
            if(success is not null && success.Current.Name=="Success") {
                var text=Text(success);
                if(!text.Contains("\u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01") && !text.Contains("saved",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unrecognized confirmation after Save settings; inspect the target dialog.");
                Click(success,"OK");return null;
            }
            return main.Current.IsEnabled?main:null;
        },"save directory confirmation",10,()=> {if(main.Current.IsEnabled)SaveFolder();});
        }
        if(directoryChanged) { Report("save_directory");SaveDirectory(); }
        Report("download");
        var mainHandle=(IntPtr)main.Current.NativeWindowHandle;
        Native.DownloadSnapshot ReadDownload() {
            var snapshot=Native.ReadDownloadSnapshot(mainHandle);
            if(!snapshot.Responsive)return snapshot;
            // Native multiline text reads have a timeout and do not enumerate every preview row.
            if(snapshot.Text.Length>0 && snapshot.SaveEnabled.HasValue && snapshot.DownloadEnabled.HasValue)return snapshot;
            var text=snapshot.Text.Length>0?snapshot.Text:Text(main);
            var buttons=Controls(main,ControlType.Button);
            bool? Enabled(string caption)=>buttons.FirstOrDefault(e=>Normalize(e.Current.Name)==Normalize(caption))?.Current.IsEnabled;
            return new(text,true,snapshot.DownloadEnabled ?? Enabled("2. \u0e14\u0e36\u0e07\u0e02\u0e49\u0e2d\u0e21\u0e39\u0e25"),snapshot.SaveEnabled ?? Enabled("3. \u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e44\u0e1f\u0e25\u0e4c txt"));
        }
        var previous=ReadDownload();
        void Download() { Click(main,"2. \u0e14\u0e36\u0e07\u0e02\u0e49\u0e2d\u0e21\u0e39\u0e25"); }
        Download();
        var observedActivity=false;
        var watch=Stopwatch.StartNew();var nextReport=TimeSpan.Zero;
        DateTime? readyAt=null;string? readySignature=null;
        Wait(()=> {
            var snapshot=ReadDownload();
            var progress=DownloadProgress.Parse(snapshot.Text);
            if(!snapshot.Responsive || snapshot.DownloadEnabled==false || progress.Busy || snapshot.Text!=previous.Text)observedActivity=true;
            if(progress.Failed)throw new InvalidOperationException("Download reported an error. Inspect the target log; export was not clicked.");
            var ready=observedActivity && snapshot.Responsive && !progress.Busy && progress.Ready && snapshot.SaveEnabled==true && snapshot.DownloadEnabled!=false;
            var signature=$"{progress.Rows}/{progress.Prepared}";
            if(!ready || readySignature!=signature){readyAt=null;readySignature=signature;}
            if(ready)readyAt ??=DateTime.UtcNow;
            if(watch.Elapsed>=nextReport) {
                Report($"download: waiting {watch.Elapsed.TotalSeconds:0}s; responsive={snapshot.Responsive}; rows={progress.Rows?.ToString() ?? "unknown"}; prepared={progress.Prepared?.ToString() ?? "unknown"}; ready={progress.Ready}; saveEnabled={snapshot.SaveEnabled?.ToString() ?? "unknown"}");
                nextReport=watch.Elapsed+TimeSpan.FromSeconds(5);
            }
            return readyAt.HasValue && DateTime.UtcNow-readyAt.Value>=TimeSpan.FromSeconds(2)?main:null;
        },"fresh completed download (Thai/English status or matching prepared counts)",settings.DownloadTimeoutSeconds,()=> {
            var snapshot=ReadDownload();
            // Never restart a download that has shown activity: the real exporter may keep buttons enabled.
            if(!observedActivity && snapshot.Responsive && snapshot.DownloadEnabled==true) {previous=ReadDownload();Download();}
            else Report("download: still waiting for the active download; no duplicate click sent");
        });
        Report("export");
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
        },"new, stable TXT export",30,()=> {
            // Never export again when a new file is already being written.
            var changedFile=Directory.GetFiles(settings.ExportDirectory,"*.txt").Any(p=>!before.TryGetValue(p,out var old) || new FileInfo(p).LastWriteTimeUtc!=old);
            if(!changedFile && main.Current.IsEnabled)Click(main,"3. \u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e44\u0e1f\u0e25\u0e4c txt");
        });
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
