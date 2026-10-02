// Local fixture only. Never connects to WEB8 or a production fingerprint database.
using System.Drawing;
using Forms=System.Windows.Forms;
namespace CJFingerService;
internal sealed class DemoTarget:Forms.Form {
    public DemoTarget() {
        Text="Text File - Time Access Solution V1.0 Area Select - TEST FIXTURE"; Width=660; Height=530;
        var from=new Forms.DateTimePicker { Left=40,Top=90,Width=130,Format=Forms.DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy" };
        var to=new Forms.DateTimePicker { Left=190,Top=90,Width=130,Format=Forms.DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy" };
        var output=new Forms.TextBox { Left=10,Top=135,Width=620,Height=285,Multiline=true,ReadOnly=true };
        var testRoot=Environment.GetEnvironmentVariable("CJ_FINGER_SERVICE_DATA");
        var folder=new Forms.TextBox { Left=40,Top=450,Width=450,Text=testRoot is not null?Path.Combine(testRoot,"exports"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CJFingerDummy","exports") };
        Directory.CreateDirectory(folder.Text);
        var login=new Forms.Button { Text="1. \u0e25\u0e47\u0e2d\u0e01\u0e2d\u0e34\u0e19\u0e40\u0e02\u0e49\u0e32\u0e23\u0e30\u0e1a\u0e1a",Left=280,Top=55,Width=160 };
        var download=new Forms.Button { Text="2. \u0e14\u0e36\u0e07\u0e02\u0e49\u0e2d\u0e21\u0e39\u0e25",Left=325,Top=90,Width=110 };
        var export=new Forms.Button { Text="3. \u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e44\u0e1f\u0e25\u0e4c txt",Left=440,Top=90,Width=150,Enabled=false };
        var save=new Forms.Button { Text="\u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e04\u0e48\u0e32",Left=530,Top=450,Width=90 };
        Controls.AddRange([from,to,output,folder,login,download,export,save]);
        Controls.Add(new Forms.Label { Left=12,Top=12,AutoSize=true,Text="DUMMY ONLY | Username: test-user | Password: test-password" });
        Controls.Add(new Forms.Label { Left=40,Top=70,AutoSize=true,Text="From                     To" });
        Controls.Add(new Forms.Label { Left=40,Top=427,AutoSize=true,Text="TXT export folder" });
        var session=false;
        var folderSaved=false;
        var downloadAttempts=0;
        var exportAttempts=0;
        var complete=false;
        var statusMode=Environment.GetEnvironmentVariable("CJ_FINGER_DEMO_STATUS") ?? "english";
        var slowSeconds=int.TryParse(Environment.GetEnvironmentVariable("CJ_FINGER_DEMO_DELAY"),out var delay)?Math.Clamp(delay,1,120):1;
        login.Click+=(_,_)=> {
            using var dialog=new Forms.Form { Text="Login Session",Width=650,Height=500,StartPosition=Forms.FormStartPosition.CenterParent };
            var username=new Forms.TextBox { Left=200,Top=100,Width=220,AccessibleName="Username" };
            var password=new Forms.TextBox { Left=200,Top=150,Width=220,UseSystemPasswordChar=true,AccessibleName="Password" };
            var submit=new Forms.Button { Left=200,Top=200,Width=220,Text="Log in" };
            var use=new Forms.Button { Dock=Forms.DockStyle.Bottom,Height=35,Text="\u0e43\u0e0a\u0e49 Session \u0e19\u0e35\u0e49",Enabled=false };
            dialog.Controls.AddRange([username,password,submit,use]);
            dialog.Controls.Add(new Forms.Label { Left=70,Top=103,AutoSize=true,Text="Username" });
            dialog.Controls.Add(new Forms.Label { Left=70,Top=153,AutoSize=true,Text="Password" });
            var error=new Forms.Label { Left=70,Top=245,AutoSize=true,ForeColor=Color.Firebrick };
            dialog.Controls.Add(error);
            submit.Click+=(_,_)=> {
                if(username.Text!="test-user" || password.Text!="test-password") { error.Text="Use test-user / test-password"; return; }
                error.Text="";
                username.Visible=password.Visible=submit.Visible=false;
                dialog.Controls.Add(new Forms.Label { Left=50,Top=70,AutoSize=true,Text="\u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01\u0e2a\u0e33\u0e40\u0e23\u0e47\u0e08 \u0e23\u0e30\u0e1a\u0e1a\u0e25\u0e47\u0e2d\u0e01\u0e2d\u0e34\u0e19\u0e40\u0e23\u0e35\u0e22\u0e1a\u0e23\u0e49\u0e2d\u0e22\u0e41\u0e25\u0e49\u0e27" }); use.Enabled=true;
            };
            use.Click+=(_,_)=> { session=true; dialog.Close(); };
            dialog.ShowDialog(this);
            if(session && Environment.GetEnvironmentVariable("CJ_FINGER_DEMO_NO_SESSION_POPUP")!="1")
                Forms.MessageBox.Show(this,"\u0e1a\u0e31\u0e19\u0e17\u0e36\u0e01 Session \u0e41\u0e25\u0e49\u0e27 \u0e41\u0e25\u0e30\u0e42\u0e2b\u0e25\u0e14 Area \u0e40\u0e23\u0e35\u0e22\u0e1a\u0e23\u0e49\u0e2d\u0e22","Success",Forms.MessageBoxButtons.OK,Forms.MessageBoxIcon.Information);
        };
        download.Click+=async(_,_)=> {
            if(!session) { output.Text="Please log in and use the session first."; return; }
            if(!folderSaved) {output.Text="Save the export directory first.";return;}
            downloadAttempts++;
            if(Environment.GetEnvironmentVariable("CJ_FINGER_DEMO_STALL")=="1" || (Environment.GetEnvironmentVariable("CJ_FINGER_DEMO_RETRY")=="1" && downloadAttempts==1))return;
            if(from.Value.Date>to.Value.Date) { output.Text="From date must be on or before To date."; return; }
            complete=false;
            if(testRoot is not null)File.WriteAllText(Path.Combine(testRoot,"fixture-download-count.txt"),downloadAttempts.ToString());
            download.Enabled=statusMode=="counts"; export.Enabled=statusMode=="counts";
            output.Text=statusMode=="counts"?"rows loaded: 2\r\nlines prepared: 0":"status: loading";
            await Task.Delay(slowSeconds*1000);
            var status=statusMode=="counts"?"":statusMode=="thai"?"status: \u0e1e\u0e23\u0e49\u0e2d\u0e21\r\n":"status: ready\r\n";
            output.Text=$"count: 2\r\nrows loaded: 2\r\nlines prepared: 2\r\n{status}TEST01 {from.Value:yyyyMMdd} 2000\r\nTEST01 {to.Value:yyyyMMdd} 0500";
            complete=true;
            download.Enabled=true; export.Enabled=true;
        };
        export.Click+=(_,_)=> {
            if(!complete){if(testRoot is not null)File.WriteAllText(Path.Combine(testRoot,"early-export.txt"),"Export was clicked before data finished loading.");return;}
            exportAttempts++;
            if(Environment.GetEnvironmentVariable("CJ_FINGER_DEMO_RETRY")=="1" && exportAttempts==1)return;
            try {
            Directory.CreateDirectory(folder.Text);
            File.WriteAllText(Path.Combine(folder.Text,$"All-attlogd{DateTime.Now:yyyy-MM-ddHHmmssfff}.txt"),$"TEST01 {from.Value:yyyyMMdd} 2000\nTEST01 {to.Value:yyyyMMdd} 0500\n");
            output.AppendText("\r\nTXT saved to: "+folder.Text);
            } catch(Exception ex) { output.Text="Export failed: "+ex.Message; }
        };
        save.Click+=(_,_)=> {
            try { Directory.CreateDirectory(folder.Text);folderSaved=true; output.Text="Export folder ready: "+folder.Text;
                Forms.MessageBox.Show(this,"Export directory saved.","Success",Forms.MessageBoxButtons.OK,Forms.MessageBoxIcon.Information);
            }
            catch(Exception ex) { output.Text="Invalid folder: "+ex.Message; }
        };
    }
}
