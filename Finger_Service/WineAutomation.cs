using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CJFingerService;

internal sealed class WineExportAutomation(Settings settings,Action<string> stage,CancellationToken cancel) {
    private int processId;
    private static string Normalize(string value)=>Regex.Replace(value,@"\s+","").ToLowerInvariant();
    private void Delay(int ms) {if(cancel.WaitHandle.WaitOne(ms))cancel.ThrowIfCancellationRequested();}
    private void Report(string message) {
        stage(message);
        File.AppendAllText(Path.Combine(Settings.Root,"service.log"),$"{DateTimeOffset.Now:O} WINE {message}\n");
    }
    private WineControl? Window(string title) {
        var handle=Native.FindWindow(processId,title);
        return handle==IntPtr.Zero?null:new(handle);
    }
    private T Wait<T>(Func<T?> action,string description,int seconds=30,Action? retry=null) where T:class {
        for(var attempt=1;attempt<=(retry is null?1:3);attempt++) {
            var until=DateTime.UtcNow.AddSeconds(seconds);
            while(DateTime.UtcNow<until) {cancel.ThrowIfCancellationRequested();Native.AssertDesktop();var result=action();if(result is not null)return result;Delay(500);}
            if(retry is not null && attempt<3) {Report($"Retry {attempt+1}/3: {description}");retry();}
        }
        throw new TimeoutException("Wine: timed out waiting for "+description);
    }
    private void Front(WineControl window) {
        for(var attempt=0;attempt<3;attempt++) {
            cancel.ThrowIfCancellationRequested();Native.AssertDesktop();Native.ShowWindow(window.Handle,9);Native.SetForegroundWindow(window.Handle);Delay(350);
            if(Native.GetForegroundWindow()==window.Handle)return;
        }
        throw new InvalidOperationException("FOCUS_LOST: cannot activate the Wine export window.");
    }
    private WineControl[] Controls(WineControl parent,string type)=>Native.ChildControls(parent.Handle).Where(c=>c.ClassName.Contains(type,StringComparison.OrdinalIgnoreCase)).ToArray();
    private sealed record OcrLine(string text,double x,double y);
    private OcrLine[] ReadOcr(WineControl window) {
        if(settings.OcrBridgeToken.Length<32)throw new InvalidOperationException("Wine requires the Ubuntu OCR bridge for browser controls. Set Local OCR token in Settings > Export.");
        Front(window);
        var path=Path.Combine(Settings.Root,"wine-ocr-"+Guid.NewGuid().ToString("N")+".png");
        try {
            Native.Screenshot(window.Handle,path);
            using var client=new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler {UseProxy=false}) {Timeout=TimeSpan.FromSeconds(25)};
            using var request=new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post,"http://127.0.0.1:17863/ocr");
            request.Headers.Add("X-OCR-Token",settings.OcrBridgeToken);
            request.Content=new System.Net.Http.ByteArrayContent(File.ReadAllBytes(path));
            request.Content.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            using var response=client.SendAsync(request,cancel).GetAwaiter().GetResult();
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"Ubuntu OCR returned {(int)response.StatusCode}. Check the bridge and local OCR token.");
            return JsonSerializer.Deserialize<OcrLine[]>(response.Content.ReadAsStringAsync(cancel).GetAwaiter().GetResult()) ?? [];
        } catch(System.Net.Http.HttpRequestException e) {throw new InvalidOperationException("Start the Ubuntu OCR bridge on this computer before running Wine automation.",e);}
        finally {if(File.Exists(path))File.Delete(path);}
    }
    private string Text(WineControl window) {
        var native=string.Join("\n",Native.ChildControls(window.Handle).Where(c=>!c.IsPassword).Select(c=>c.Name));
        // Browser-rendered text is not a native child HWND. Read it optically.
        return Controls(window,"internet explorer").Length>0 || Controls(window,"chrome").Length>0 || native.Trim().Length==0
            ? native+"\n"+string.Join("\n",ReadOcr(window).Select(l=>l.text)) : native;
    }
    private void Click(WineControl window,string caption) {
        Front(window);
        var matches=Controls(window,"button").Where(c=>Normalize(c.Name.Replace("&",""))==Normalize(caption)).ToArray();
        if(matches.Length>1)throw new InvalidOperationException("Ambiguous Wine button: "+caption);
        if(matches.Length==1) {
            if(!window.IsEnabled || !matches[0].IsEnabled)throw new InvalidOperationException("Button is disabled: "+caption);
            if(!Native.PostMessage(matches[0].Handle,0x00F5,IntPtr.Zero,IntPtr.Zero))throw new InvalidOperationException("Cannot click Wine button: "+caption);
            return;
        }
        var lines=ReadOcr(window).Where(l=>Normalize(l.text)==Normalize(caption)).ToArray();
        if(lines.Length!=1)throw new InvalidOperationException("Cannot uniquely locate Wine OCR button: "+caption);
        var bounds=window.BoundingRectangle;
        Native.Click(window.Handle,bounds.Left+lines[0].x,bounds.Top+lines[0].y);
    }
    private void FillNative(WineControl window,WineControl field,string value) {
        Front(window);
        if(!field.IsEnabled || Native.ReadOnlyEdit(field.Handle))throw new InvalidOperationException("Wine input is not editable.");
        var bounds=field.BoundingRectangle;
        Native.Click(window.Handle,bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2);Delay(150);
        void Verify() {cancel.ThrowIfCancellationRequested();if(!Native.HasNativeFocus(window.Handle,field.Handle))throw new LoginRetryException("FOCUS_LOST: Wine input changed; typing stopped.");}
        for(var i=0;i<4;i++){Verify();Delay(100);}
        Native.ReplaceFocusedText(value,Verify);Delay(350);Verify();
        if(field.IsPassword) {
            if(Native.EditTextLength(field.Handle)!=value.Length)throw new LoginRetryException("Password length verification failed.");
        } else if(field.Name!=value)throw new LoginRetryException("Input verification failed.");
    }
    private void SetValue(WineControl window,WineControl field,string value)=>FillNative(window,field,value);
    private sealed class LoginRetryException(string message):Exception(message);
    private bool LoginSucceeded(WineControl login) {
        var text=Normalize(Text(login));
        var success=text.Contains(Normalize("\u0e23\u0e30\u0e1a\u0e1a\u0e25\u0e47\u0e2d\u0e01\u0e2d\u0e34\u0e19\u0e40\u0e23\u0e35\u0e22\u0e1a\u0e23\u0e49\u0e2d\u0e22\u0e41\u0e25\u0e49\u0e27")) || text.Contains(Normalize("\u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e2a\u0e33\u0e40\u0e23\u0e47\u0e08"));
        return success && Controls(login,"button").Any(c=>c.IsEnabled && Normalize(c.Name)==Normalize("\u0e43\u0e0a\u0e49 Session \u0e19\u0e35\u0e49"));
    }
    private static bool Rejected(string text)=>Regex.IsMatch(text,@"login failed|invalid (username|password|credentials)|incorrect (username|password)|authentication failed",RegexOptions.IgnoreCase) || text.Contains("\u0e23\u0e2b\u0e31\u0e2a\u0e1c\u0e48\u0e32\u0e19\u0e44\u0e21\u0e48\u0e16\u0e39\u0e01\u0e15\u0e49\u0e2d\u0e07");
    private WineControl Authenticate() {
        for(var attempt=1;attempt<=3;attempt++) {
            var login=Window("Login Session") ?? throw new InvalidOperationException("Login window closed unexpectedly.");
            try {
                if(LoginSucceeded(login))return login;
                if(attempt>1)Report($"Retry {attempt}/3: login; reacquiring both fields");
                var edits=Controls(login,"edit").Where(c=>c.IsEnabled && !Native.ReadOnlyEdit(c.Handle)).ToArray();
                var users=edits.Where(c=>!c.IsPassword).ToArray();var passwords=edits.Where(c=>c.IsPassword).ToArray();
                if(users.Length==1 && passwords.Length==1) {
                    FillNative(login,users[0],settings.Username);
                    FillNative(login,passwords[0],Settings.Unprotect(settings.PasswordProtected));
                    if(users[0].Name!=settings.Username)throw new LoginRetryException("Username changed; credentials not submitted.");
                } else if(edits.Length==0) FillBrowserLogin(login);
                else throw new LoginRetryException("Login inputs are ambiguous; credentials not submitted.");
                Click(login,"Log in");
                var submitted=DateTime.UtcNow;
                Wait(()=> {
                    if(LoginSucceeded(login))return login;
                    foreach(var title in new[]{"Error","Login failed","Authentication failed"}) {
                        var popup=Window(title);
                        if(popup is not null && Rejected(Text(popup))) {Click(popup,"OK");throw new LoginRetryException("Login rejected.");}
                    }
                    if(DateTime.UtcNow-submitted>TimeSpan.FromSeconds(2) && Rejected(Text(login)))throw new LoginRetryException("Login rejected.");
                    return null;
                },"successful login",45);
                return login;
            } catch(Exception e) when(e is LoginRetryException or TimeoutException || e is InvalidOperationException && e.Message.StartsWith("FOCUS_LOST:")) {
                Report($"Retry check {attempt}/3: login incomplete ({e.GetType().Name}); no session selected");
                if(attempt==3)throw new InvalidOperationException("LOGIN_FAILED: stopped after 3 attempts. Check Wine input focus and OCR. No session was selected.",e);
                Delay(1500);
            }
        }
        throw new InvalidOperationException("LOGIN_FAILED");
    }
    private void FillBrowserLogin(WineControl login) {
        var lines=ReadOcr(login);
        var userLabels=lines.Where(l=>Normalize(l.text).TrimEnd(':')=="username").ToArray();
        var passwordLabels=lines.Where(l=>Normalize(l.text).TrimEnd(':')=="password").ToArray();
        if(userLabels.Length!=1 || passwordLabels.Length!=1)throw new LoginRetryException("OCR login labels are missing or ambiguous.");
        var path=Path.Combine(Settings.Root,"wine-fields-"+Guid.NewGuid().ToString("N")+".png");
        try {
            Native.Screenshot(login.Handle,path);
            Rectangle username,password;
            using(var image=new Bitmap(path)) {
                username=WineLoginVision.FindField(image,new Point((int)userLabels[0].x,(int)userLabels[0].y));
                password=WineLoginVision.FindField(image,new Point((int)passwordLabels[0].x,(int)passwordLabels[0].y));
            }
            if(username.IntersectsWith(password))throw new LoginRetryException("OCR login fields overlap.");
            var original=login.BoundingRectangle;
            void Fill(Rectangle relative,string value) {
                Front(login);var field=relative;field.Offset(original.Location);
                if(login.BoundingRectangle!=original)throw new LoginRetryException("Login window moved; reacquire inputs.");
                Native.Click(login.Handle,field.Left+field.Width/2,field.Top+field.Height/2);Delay(200);
                void Verify() {
                    cancel.ThrowIfCancellationRequested();
                    if(login.BoundingRectangle!=original || !Native.HasCaretIn(login.Handle,field))throw new LoginRetryException("FOCUS_LOST: Wine browser does not expose a caret inside the selected input; typing stopped.");
                }
                for(var i=0;i<4;i++){Verify();Delay(100);}
                Native.ReplaceFocusedText(value,Verify);Delay(300);Verify();
            }
            Fill(username,settings.Username);
            bool UserMatches()=>ReadOcr(login).Any(l=>username.Contains((int)l.x,(int)l.y) && l.text==settings.Username);
            if(!UserMatches())throw new LoginRetryException("OCR username verification failed; credentials not submitted.");
            var secret=Settings.Unprotect(settings.PasswordProtected);
            Fill(password,secret);
            Native.Screenshot(login.Handle,path);
            using(var image=new Bitmap(path))if(WineLoginVision.MaskedCharacterCount(image,password)!=secret.Length)
                throw new LoginRetryException("Password mask count verification failed; credentials not submitted.");
            if(!UserMatches())throw new LoginRetryException("Username changed; credentials not submitted.");
        } finally {if(File.Exists(path))File.Delete(path);}
    }
    private void SetDate(WineControl control,DateTime date) {
        Native.EnterDate(control.Handle,date);
        for(var i=0;i<20;i++) {
            var actual=control.Name;
            if(DateTime.TryParseExact(actual,"dd/MM/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed) && parsed.Date==date.Date)return;
            if(DateTime.TryParse(actual,CultureInfo.CurrentCulture,DateTimeStyles.None,out parsed) && parsed.Date==date.Date)return;
            Delay(100);
        }
        throw new InvalidOperationException("Wine date verification failed; export stopped before downloading.");
    }

    public string Run() {
        settings.Validate(false); Native.AssertDesktop();
        var processName=Path.GetFileNameWithoutExtension(settings.ProgramPath);
        var candidates=Process.GetProcessesByName(processName).Where(p=> { try { return string.Equals(p.MainModule?.FileName,settings.ProgramPath,StringComparison.OrdinalIgnoreCase) && p.MainWindowTitle.Contains("Time Access Solution"); } catch { return false; } }).ToArray();
        if(candidates.Length>1) throw new InvalidOperationException("Several export windows are running. Keep one instance only.");
        using var process=candidates.FirstOrDefault() ?? Process.Start(new ProcessStartInfo(settings.ProgramPath) { UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(settings.ProgramPath)!,WindowStyle=ProcessWindowStyle.Normal })!;
        processId=process.Id;
        var main=Wait(()=>Window("Time Access Solution"),"export main window");
        if(settings.SkipLogin) {
            Report("skip_login");
            if(Window("Login Session") is not null || !main.Current.IsEnabled)
                throw new InvalidOperationException("SKIP_LOGIN_BLOCKED: close WEB8 dialogs and ensure it is ready to download, then retry.");
        } else {
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
        }
        Front(main);
        Report("select_dates");
        var dates=Controls(main,"SysDateTimePick32").OrderBy(e=>e.BoundingRectangle.Left).ToArray();
        if(dates.Length!=2) throw new InvalidOperationException("Expected two native date pickers. Run Diagnostics to inspect this program version.");
        var today=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"SE Asia Standard Time").Date;
        SetDate(dates[0],today.AddDays(-settings.LookbackDays));
        SetDate(dates[1],today);
        var folder=Controls(main,"edit").Where(e=>e.IsEnabled && !e.IsPassword && !Native.ReadOnlyEdit(e.Handle)).OrderByDescending(e=>e.Current.BoundingRectangle.Top).FirstOrDefault();
        if(folder==null || folder.Current.BoundingRectangle.Top < main.Current.BoundingRectangle.Top+main.Current.BoundingRectangle.Height*.7) throw new InvalidOperationException("Cannot locate export directory input.");
        var directoryChanged=!string.Equals(folder.Name,settings.ExportDirectory,StringComparison.OrdinalIgnoreCase);
        if(directoryChanged) SetValue(main,folder,settings.ExportDirectory);
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
            var buttons=Controls(main,"button");
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
        ExportAutomation.ValidateFile(file); return file;
    }
}
