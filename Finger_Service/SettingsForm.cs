using System.Drawing;
using Forms = System.Windows.Forms;
using Microsoft.Win32;
namespace CJFingerService;
internal sealed class SettingsForm : Forms.Form {
    public Settings Value { get; private set; }
    public SettingsForm(Settings initial) {
        Value=initial; Text="CJ Finger Service · ตั้งค่า"; Width=620; Height=565; MinimumSize=new Size(580,510); StartPosition=Forms.FormStartPosition.CenterScreen;
        Font=new Font("Segoe UI",10); BackColor=Color.White;
        var layout=new Forms.TableLayoutPanel { Dock=Forms.DockStyle.Fill,Padding=new Forms.Padding(12),ColumnCount=3,AutoScroll=true };
        layout.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute,180)); layout.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent,100)); layout.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute,60));
        Controls.Add(layout);
        Forms.TextBox Field(string caption,string value,bool password=false) { var row=layout.RowCount++; layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize)); layout.Controls.Add(new Forms.Label {Text=caption,AutoSize=true,Margin=new Forms.Padding(0,5,8,5)},0,row); var input=new Forms.TextBox { Text=value,Dock=Forms.DockStyle.Fill,UseSystemPasswordChar=password,Margin=new Forms.Padding(0,3,5,3) }; layout.Controls.Add(input,1,row); return input; }
        var program=Field("โปรแกรม WEB8 (.exe)",initial.ProgramPath); var choose=new Forms.Button { Text="เลือก",AutoSize=true }; layout.Controls.Add(choose,2,0);
        choose.Click+=(_,_)=> { using var dialog=new Forms.OpenFileDialog { Filter="Program (*.exe)|*.exe" }; if(dialog.ShowDialog()==Forms.DialogResult.OK) program.Text=dialog.FileName; };
        var folder=Field("โฟลเดอร์ Export TXT",initial.ExportDirectory); var browse=new Forms.Button { Text="เลือก",AutoSize=true }; layout.Controls.Add(browse,2,1);
        browse.Click+=(_,_)=> { using var dialog=new Forms.FolderBrowserDialog { SelectedPath=folder.Text }; if(dialog.ShowDialog()==Forms.DialogResult.OK) folder.Text=dialog.SelectedPath; };
        var user=Field("Username",initial.Username);
        var password=Field("Password",SafeRead(initial.PasswordProtected),true);
        var server=Field("Server URL",initial.ServerUrl);
        var token=Field("Service token",SafeRead(initial.TokenProtected),true);
        var upload=new Forms.CheckBox { Text="ส่ง TXT ไปยัง Server",Checked=initial.EnableUpload,AutoSize=true };
        var http=new Forms.CheckBox { Text="อนุญาต HTTP ใน LAN (ไม่เข้ารหัส)",Checked=initial.AllowHttp,AutoSize=true };
        layout.Controls.Add(upload,1,layout.RowCount++); layout.SetColumnSpan(upload,2);
        layout.Controls.Add(http,1,layout.RowCount++); layout.SetColumnSpan(http,2);
        var testServer=new Forms.Button { Text="Test connection",AutoSize=true };
        layout.Controls.Add(testServer,1,layout.RowCount++);
        testServer.Click+=async(_,_)=> {
            testServer.Enabled=false;
            try { await UploadClient.Test(new Settings {ServerUrl=server.Text.Trim(),TokenProtected=Settings.Protect(token.Text.Trim()),AllowHttp=http.Checked}); Forms.MessageBox.Show("เชื่อมต่อ Server สำเร็จ / Connected successfully"); }
            catch(Exception e) { Forms.MessageBox.Show(e.Message,"Connection failed"); }
            finally { if(!testServer.IsDisposed) testServer.Enabled=true; }
        };
        var days=Field("ย้อนหลัง (วัน)",initial.LookbackDays.ToString());
        var timeout=Field("รอดึงข้อมูลสูงสุด (วินาที)",initial.DownloadTimeoutSeconds.ToString());
        var language=Field("OCR language",initial.OcrLanguage);
        var startAt=new Forms.DateTimePicker { Format=Forms.DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy HH:mm",ShowUpDown=true,ShowCheckBox=true,Checked=initial.ScheduleStartAt.HasValue,Value=initial.ScheduleStartAt ?? DateTime.Now.AddMinutes(1),Dock=Forms.DockStyle.Fill };
        var startRow=layout.RowCount++;
        layout.Controls.Add(new Forms.Label { Text="เวลาเริ่ม (ไม่ติ๊ก = ทันที)",AutoSize=true },0,startRow);
        layout.Controls.Add(startAt,1,startRow); layout.SetColumnSpan(startAt,2);
        var schedule=new Forms.CheckBox { Text="เริ่มรอบอัตโนมัติทุก 30 นาที",Checked=initial.AutoStartSchedule,AutoSize=true };
        var startup=new Forms.CheckBox { Text="เปิด System tray เมื่อเข้า Windows",Checked=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")?.GetValue("CJFingerService")!=null,AutoSize=true };
        layout.Controls.Add(schedule,1,layout.RowCount++); layout.SetColumnSpan(schedule,2); layout.Controls.Add(startup,1,layout.RowCount++); layout.SetColumnSpan(startup,2);
        var note=new Forms.Label { AutoSize=true,MaximumSize=new Size(550,0),ForeColor=Color.DarkSlateGray,Text="เริ่มตามเวลาของ Windows แล้วทำซ้ำทุก 30 นาที\nต้องไม่ล็อกหน้าจอระหว่างรัน · เก็บไฟล์เดิมและส่งซ้ำเมื่อส่งไม่สำเร็จ\nรหัสผ่านและ Service token เข้ารหัสด้วย Windows DPAPI" };
        layout.Controls.Add(note,0,layout.RowCount++); layout.SetColumnSpan(note,3);
        var buttons=new Forms.FlowLayoutPanel { AutoSize=true,Dock=Forms.DockStyle.Fill };
        var save=new Forms.Button { Text="Save",AutoSize=true };
        var start=new Forms.Button { Text="▶ Start",AutoSize=true,BackColor=Color.FromArgb(0,120,181),ForeColor=Color.White };
        buttons.Controls.AddRange([save,start]); layout.Controls.Add(buttons,1,layout.RowCount++); layout.SetColumnSpan(buttons,2);
        void Commit(bool startSchedule) {
            if(!int.TryParse(days.Text,out var lookback)||lookback<0||lookback>31||!int.TryParse(timeout.Text,out var wait)||wait<30||wait>1800) { Forms.MessageBox.Show("Lookback: 0–31 days. Timeout: 30–1800 seconds."); return; }
            Value=new Settings {ProgramPath=program.Text.Trim(),ExportDirectory=folder.Text.Trim(),Username=user.Text.Trim(),PasswordProtected=Settings.Protect(password.Text),TokenProtected=Settings.Protect(token.Text.Trim()),ServerUrl=server.Text.Trim(),EnableUpload=upload.Checked,AllowHttp=http.Checked,LookbackDays=lookback,DownloadTimeoutSeconds=wait,OcrLanguage=language.Text.Trim(),AutoStartSchedule=startSchedule||schedule.Checked,ScheduleStartAt=startAt.Checked?startAt.Value:null};
            if(Value.EnableUpload) { try { _=UploadClient.BaseUri(Value); } catch(Exception e) { Forms.MessageBox.Show(e.Message); return; } }
            if(Value.AutoStartSchedule) { try { Value.Validate(false); } catch(Exception e) { Forms.MessageBox.Show(e.Message); return; } }
            Value.Save();
            using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if(startup.Checked) key.SetValue("CJFingerService",$"\"{Environment.ProcessPath}\""); else key.DeleteValue("CJFingerService",false);
            DialogResult=Forms.DialogResult.OK; Close();
        }
        save.Click+=(_,_)=>Commit(false);
        start.Click+=(_,_)=>Commit(true);
        AcceptButton=save;
    }
    private static string SafeRead(string encrypted) { try { return Settings.Unprotect(encrypted); } catch { return ""; } }
}
