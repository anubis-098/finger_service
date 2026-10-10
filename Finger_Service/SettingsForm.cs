using System.Drawing;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace CJFingerService;

internal sealed class SettingsForm : Forms.Form {
    public Settings Value { get; private set; }
    public bool StartRequested { get; private set; }

    public SettingsForm(Settings initial) {
        Value=initial;
        Text=$"CJ Finger Service {Updater.VersionText}";
        ClientSize=new Size(560,410);
        MinimumSize=new Size(560,440);
        StartPosition=Forms.FormStartPosition.CenterScreen;
        Font=new Font("Segoe UI",9);
        BackColor=Color.White;

        var actions=new Forms.FlowLayoutPanel {Dock=Forms.DockStyle.Bottom,Height=48,Padding=new Forms.Padding(8),FlowDirection=Forms.FlowDirection.RightToLeft};
        var start=new Forms.Button {Text="Start",Width=90,Height=30,BackColor=Color.FromArgb(0,120,181),ForeColor=Color.White};
        var save=new Forms.Button {Text="Save",Width=90,Height=30};
        actions.Controls.AddRange([start,save]);
        var tabs=new Forms.TabControl {Dock=Forms.DockStyle.Fill};
        Controls.Add(tabs); Controls.Add(actions);

        Forms.TableLayoutPanel Tab(string name) {
            var page=new Forms.TabPage(name) {BackColor=Color.White,AccessibleName=name};
            var panel=new Forms.TableLayoutPanel {Dock=Forms.DockStyle.Fill,Padding=new Forms.Padding(12),AutoScroll=true,ColumnCount=3,AccessibleName=name};
            panel.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute,135));
            panel.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent,100));
            panel.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute,66));
            page.Controls.Add(panel); tabs.TabPages.Add(page); return panel;
        }
        void Row(Forms.TableLayoutPanel panel,string label,Forms.Control input) {
            var row=panel.RowCount++; panel.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
            panel.Controls.Add(new Forms.Label {Text=label,AutoSize=true,Margin=new Forms.Padding(0,7,5,7)},0,row);
            input.Dock=Forms.DockStyle.Fill; input.Margin=new Forms.Padding(0,4,5,4); panel.Controls.Add(input,1,row); panel.SetColumnSpan(input,2);
        }
        Forms.TextBox Field(Forms.TableLayoutPanel panel,string label,string value,bool secret=false) {
            var input=new Forms.TextBox {Text=value,UseSystemPasswordChar=secret}; Row(panel,label,input);return input;
        }
        void Note(Forms.TableLayoutPanel panel,string text) {
            var label=new Forms.Label {Text=text,AutoSize=true,MaximumSize=new Size(505,0),ForeColor=Color.DimGray,Margin=new Forms.Padding(0,12,0,8)};
            var row=panel.RowCount++; panel.Controls.Add(label,0,row);panel.SetColumnSpan(label,3);
        }

        var export=Tab("Export");
        var program=Field(export,"WEB8 program",initial.ProgramPath);
        export.SetColumnSpan(program,1);
        var choose=new Forms.Button {Text="Browse",AutoSize=true};export.Controls.Add(choose,2,0);
        choose.Click+=(_,_)=> {using var picker=new Forms.OpenFileDialog {Filter="Programs (*.exe)|*.exe"};if(picker.ShowDialog()==Forms.DialogResult.OK)program.Text=picker.FileName;};
        var folder=Field(export,"TXT folder",initial.ExportDirectory);
        export.SetColumnSpan(folder,1);
        var browse=new Forms.Button {Text="Browse",AutoSize=true};export.Controls.Add(browse,2,1);
        browse.Click+=(_,_)=> {using var picker=new Forms.FolderBrowserDialog {SelectedPath=folder.Text};if(picker.ShowDialog()==Forms.DialogResult.OK)folder.Text=picker.SelectedPath;};
        var user=Field(export,"Username",initial.Username);
        var password=Field(export,"Password",SafeRead(initial.PasswordProtected),true);
        var skipLogin=new Forms.CheckBox {Text="Skip login (WEB8 already ready)",Checked=initial.SkipLogin,AutoSize=true}; Row(export,"Login",skipLogin);
        void UpdateLoginFields() { user.Enabled=password.Enabled=!skipLogin.Checked; }
        skipLogin.CheckedChanged+=(_,_)=>UpdateLoginFields(); UpdateLoginFields();
        var days=new Forms.NumericUpDown {Minimum=0,Maximum=31,Value=Math.Clamp(initial.LookbackDays,0,31)}; Row(export,"Lookback days",days);
        var timeout=new Forms.NumericUpDown {Minimum=30,Maximum=1800,Increment=30,Value=Math.Clamp(initial.DownloadTimeoutSeconds,30,1800)};Row(export,"Timeout (seconds)",timeout);
        var language=Field(export,"Target OCR language",initial.OcrLanguage);
        var ubuntuOcr=new Forms.CheckBox {Text="Ubuntu / Wine (local Tesseract bridge)",Checked=initial.UseUbuntuOcr,AutoSize=true}; Row(export,"OCR engine",ubuntuOcr);
        var ocrToken=Field(export,"Local OCR token",initial.OcrBridgeToken,true);
        Note(export,"Keep the Windows session unlocked. The target export application may come to the foreground.");

        var serverTab=Tab("Server");
        var server=Field(serverTab,"Website URL",initial.ServerUrl);
        var token=Field(serverTab,"Service token",SafeRead(initial.TokenProtected),true);
        var upload=new Forms.CheckBox {Text="Upload exported TXT files",Checked=initial.EnableUpload,AutoSize=true};Row(serverTab,"",upload);
        var http=new Forms.CheckBox {Text="Allow unencrypted HTTP on a trusted LAN",Checked=initial.AllowHttp,AutoSize=true};Row(serverTab,"",http);
        var test=new Forms.Button {Text="Test connection",AutoSize=true};Row(serverTab,"",test);
        test.Click+=async(_,_)=> {
            test.Enabled=false;
            try {await UploadClient.Test(new Settings {ServerUrl=server.Text.Trim(),TokenProtected=Settings.Protect(token.Text.Trim()),AllowHttp=http.Checked});if(!IsDisposed)Forms.MessageBox.Show("Connected successfully.");}
            catch(Exception e) {if(!IsDisposed)Forms.MessageBox.Show(e.Message,"Connection failed");}
            finally {if(!test.IsDisposed)test.Enabled=true;}
        };
        Note(serverTab,"Create a service token on the website's Fingerprint logs page. Passwords and tokens are encrypted for this Windows account. Failed uploads remain queued.");

        var schedule=Tab("Schedule");
        var startAt=new Forms.DateTimePicker {Format=Forms.DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd HH:mm",ShowUpDown=true,Value=ServiceClock.Now.DateTime};
        Row(schedule,"Start date/time",startAt);
        Note(schedule,"Schedule timezone: "+ServiceClock.Zone);
        using var registry=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        var startup=new Forms.CheckBox {Text="Open tray app when Windows starts (stopped)",AutoSize=true,Checked=registry?.GetValue("CJFingerService")!=null};Row(schedule,"",startup);
        Note(schedule,"Always opens STOPPED, including after an update or Windows restart.\n\nClick Start to begin now, or choose a future date/time to wait until then. If the selected time has already arrived, Start begins immediately. Runs repeat every 30 minutes. Save never starts automation.");
        tabs.SelectedIndex=2;

        void Commit(bool startSchedule) {
            var value=new Settings {ProgramPath=program.Text.Trim(),ExportDirectory=folder.Text.Trim(),Username=user.Text.Trim(),PasswordProtected=Settings.Protect(password.Text),TokenProtected=Settings.Protect(token.Text.Trim()),ServerUrl=server.Text.Trim(),EnableUpload=upload.Checked,AllowHttp=http.Checked,LookbackDays=(int)days.Value,DownloadTimeoutSeconds=(int)timeout.Value,OcrLanguage=language.Text.Trim(),ScheduleStartAt=startAt.Value};
            try {
                value.SkipLogin=skipLogin.Checked;
                if(skipLogin.Checked) value.PasswordProtected=initial.PasswordProtected;
                value.UseUbuntuOcr=ubuntuOcr.Checked;
                value.OcrBridgeToken=ocrToken.Text.Trim();
                if(value.UseUbuntuOcr && value.OcrBridgeToken.Length<32) throw new InvalidOperationException("Paste the local OCR bridge token (at least 32 characters).");
                if(startSchedule) { _=TrayApp.FirstRun(value,ServiceClock.Now);value.Validate(false); }
                else if(value.EnableUpload) _=UploadClient.BaseUri(value);
                using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if(startup.Checked)key.SetValue("CJFingerService",$"\"{Environment.ProcessPath}\"");else key.DeleteValue("CJFingerService",false);
                value.Save();Value=value;StartRequested=startSchedule;DialogResult=Forms.DialogResult.OK;Close();
            } catch(Exception e) {Forms.MessageBox.Show(e.Message,"Check settings");}
        }
        save.Click+=(_,_)=>Commit(false);start.Click+=(_,_)=>Commit(true);AcceptButton=save;
    }
    private static string SafeRead(string encrypted) {try{return Settings.Unprotect(encrypted);}catch{return "";}}
}
