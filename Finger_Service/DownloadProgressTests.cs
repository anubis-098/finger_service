namespace CJFingerService;
internal static class DownloadProgressTests {
    internal static void Run() {
        var unknown=DownloadProgress.Parse("count: 984");
        if(!TaskStage.WaitReason(new("count: 984",true,true,true),unknown,true).Contains("not recognized"))throw new Exception("Missing completion reason not reported.");
        var readyProgress=DownloadProgress.Parse("status: ready");
        if(!TaskStage.WaitReason(new("",true,true,false),readyProgress,true).Contains("Save TXT button"))throw new Exception("Disabled save reason not reported.");
        if(!TaskStage.WaitReason(new("",false,true,true),readyProgress,true).Contains("not responding"))throw new Exception("Unresponsive reason not reported.");
        if(TaskStage.Describe("download: waiting").Code!="D04" || TaskStage.Describe("detail: E02|Waiting for TXT").Code!="E02")throw new Exception("Diagnostic stage IDs unstable.");
        void Check(string text,bool ready,bool busy=false,bool failed=false) {
            var result=DownloadProgress.Parse(text);
            if(result.Ready!=ready || result.Busy!=busy || result.Failed!=failed)throw new Exception("Download readiness parser mismatch.");
        }
        Check("count : 984\r\nrows loaded: 984\r\nlines prepared: 984\r\nstatus: \u0e1e\u0e23\u0e49\u0e2d\u0e21",true);
        Check("status: ready",true);
        Check("rows loaded: 1,234\nlines prepared: 1,234",true);
        Check("rows loaded: 984\nlines prepared: 500\nstatus: ready",false);
        Check("rows loaded: 984\nlines prepared: 984\nstatus: loading",false,true);
        Check("status: ready\nstatus: processing",false,true);
        Check("status: ready\nstatus: error",false,false,true);
        Check("loading",false,true);
        Check("count: 984\nPreview: ready",false);
        Check("status: not ready",false);
        Check("rows loaded: 0\nlines prepared: 0\nstatus: ready",true);
        Check("status: \u0e01\u0e33\u0e25\u0e31\u0e07\u0e14\u0e36\u0e07\u0e02\u0e49\u0e2d\u0e21\u0e39\u0e25",false,true);
    }
}
