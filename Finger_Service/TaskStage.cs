namespace CJFingerService;
internal static class TaskStage {
    internal static (string Code,string Title) Describe(string text) {
        if(text.StartsWith("detail:")) {var parts=text[7..].Split('|',2);return(parts[0].Trim(),parts.Length>1?parts[1].Trim():"Working");}
        if(text.StartsWith("download:"))return("D04","Waiting for download completion");
        return text switch {
            "login"=>("L01","Opening login / selecting session"),
            "skip_login"=>("L00","Skipping login"),
            "select_dates"=>("D01","Selecting date range"),
            "save_directory"=>("D02","Saving export folder"),
            "download"=>("D03","Clicking Download"),
            "export"=>("E01","Clicking Save TXT"),
            "upload"=>("U01","Uploading queued files"),
            _=>("START","Preparing task")
        };
    }
    internal static string WaitReason(Native.DownloadSnapshot snapshot,DownloadProgress progress,bool activity) {
        if(!snapshot.Responsive)return "WEB8 window is not responding";
        if(!activity)return "No new download activity detected";
        if(progress.Busy)return "WEB8 still reports loading / processing";
        if(progress.Rows.HasValue && progress.Prepared.HasValue && progress.Rows!=progress.Prepared)return "Loaded rows and prepared lines do not match";
        if(!progress.Ready)return "Completion text not recognized; waiting for ready status or matching counts";
        if(snapshot.SaveEnabled!=true)return "Save TXT button is disabled or could not be detected";
        if(snapshot.DownloadEnabled==false)return "Download button is still disabled";
        return "Waiting for ready state to remain stable for 2 seconds";
    }
}
