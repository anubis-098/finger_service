using System.Text.RegularExpressions;
namespace CJFingerService;

// Parse summary lines only, never employee preview rows. The last status wins.
internal sealed record DownloadProgress(bool Ready,bool Busy,bool Failed,int? Rows,int? Prepared) {
    internal static DownloadProgress Parse(string text) {
        var statuses=Regex.Matches(text,@"(?im)^\s*status\s*[:：]\s*([^\r\n]*)");
        var status=statuses.Count==0?"":statuses[^1].Groups[1].Value.Trim();
        bool Has(string pattern)=>Regex.IsMatch(status,pattern,RegexOptions.IgnoreCase);
        var failed=Has(@"\b(error|failed|failure)\b|\u0e1c\u0e34\u0e14\u0e1e\u0e25\u0e32\u0e14|\u0e25\u0e49\u0e21\u0e40\u0e2b\u0e25\u0e27");
        var busy=Has(@"\b(loading|downloading|processing|preparing|busy|waiting)\b|\u0e01\u0e33\u0e25\u0e31\u0e07") || Regex.IsMatch(text.Trim(),@"^(loading|downloading|processing)[.\s]*$",RegexOptions.IgnoreCase);
        int? Count(string label) {
            var matches=Regex.Matches(text,@"(?im)^\s*"+label+@"\s*[:：]\s*([\d,]+)\s*$");
            return matches.Count>0 && int.TryParse(matches[^1].Groups[1].Value.Replace(",",""),out var value)?value:null;
        }
        var rows=Count(@"rows\s+loaded");var prepared=Count(@"lines\s+prepared");
        var ready=Has(@"^(ready|completed?|done)\b|^\u0e1e\u0e23\u0e49\u0e2d\u0e21|^\u0e40\u0e2a\u0e23\u0e47\u0e08");
        // A partial conversion must not be saved even if a stale ready line remains.
        var countsAgree=rows.HasValue && prepared.HasValue && rows==prepared;
        if(rows.HasValue && prepared.HasValue && rows!=prepared) ready=false;
        return new(!failed && !busy && (ready || (status.Length==0 && countsAgree)),busy,failed,rows,prepared);
    }
}
