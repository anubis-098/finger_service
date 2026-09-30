using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
namespace CJFingerService;
internal static class Updater {
    internal const string Repository="anubis-098/finger_service";
    internal static Version Current => Assembly.GetExecutingAssembly().GetName().Version!;
    internal static string VersionText => Current.ToString(3);
    internal sealed record Release(Version Version,string Url,string Hash);
    internal static readonly string[] Files=["CJFingerService.exe","ocr.ps1","update.ps1","README.md"];
    private static HttpClient Client() {
        var client=new HttpClient {Timeout=TimeSpan.FromMinutes(5)};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CJFingerService/"+VersionText);
        return client;
    }
    internal static Release? ParseRelease(string json) {
        using var doc=JsonDocument.Parse(json); var root=doc.RootElement;
        if(root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        if(!Version.TryParse(root.GetProperty("tag_name").GetString()?.TrimStart('v'),out var version) || version.Build<0 || version<=new Version(VersionText)) return null;
        var asset=root.GetProperty("assets").EnumerateArray().SingleOrDefault(a=>a.GetProperty("name").GetString()=="CJFingerService-win-x64.zip");
        if(asset.ValueKind==JsonValueKind.Undefined) throw new InvalidOperationException("Release is missing CJFingerService-win-x64.zip.");
        var url=asset.GetProperty("browser_download_url").GetString()!;
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Host!="github.com" || !uri.AbsolutePath.StartsWith("/"+Repository+"/releases/download/",StringComparison.Ordinal)) throw new InvalidOperationException("Invalid release download URL.");
        var digest=asset.TryGetProperty("digest",out var value)?value.GetString():null;
        if(digest is null || !System.Text.RegularExpressions.Regex.IsMatch(digest,"^sha256:[a-fA-F0-9]{64}$")) throw new InvalidOperationException("Release has no GitHub SHA-256 digest. Upload a new release asset.");
        return new Release(version,url,digest[7..]);
    }
    internal static async Task<Release?> Check() {
        using var client=Client(); using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20)); using var response=await client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest",timeout.Token);
        if(response.StatusCode==System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode(); return ParseRelease(await response.Content.ReadAsStringAsync());
    }
    internal static void Extract(string zip,string directory) {
        using var archive=ZipFile.OpenRead(zip);
        if(archive.Entries.Count!=Files.Length || archive.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Files.Length || archive.Entries.Any(e=>!Files.Contains(e.FullName,StringComparer.Ordinal) || e.Length>300_000_000)) throw new InvalidOperationException("Unexpected files in update package.");
        Directory.CreateDirectory(directory);
        foreach(var entry in archive.Entries) entry.ExtractToFile(Path.Combine(directory,entry.FullName));
    }
    internal static async Task<string> Prepare(Release release) {
        var root=Path.Combine(Settings.Root,"updates",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var zip=Path.Combine(root,"release.zip");
        using(var client=Client()) using(var response=await client.GetAsync(release.Url,HttpCompletionOption.ResponseHeadersRead)) {
            response.EnsureSuccessStatusCode();
            await using var source=await response.Content.ReadAsStreamAsync(); await using var dest=File.Create(zip);
            var buffer=new byte[81920]; long total=0; int read;
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(5));
            while((read=await source.ReadAsync(buffer,timeout.Token))>0) {total+=read;if(total>300_000_000) throw new InvalidOperationException("Update exceeds size limit.");await dest.WriteAsync(buffer.AsMemory(0,read),timeout.Token);}
        }
        await using(var stream=File.OpenRead(zip)) {
            var actual=Convert.ToHexString(await SHA256.HashDataAsync(stream));
            if(!actual.Equals(release.Hash,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Update checksum mismatch. Current version unchanged.");
        }
        var extracted=Path.Combine(root,"files"); Extract(zip,extracted);
        var version=FileVersionInfo.GetVersionInfo(Path.Combine(extracted,"CJFingerService.exe")).FileVersion;
        if(!Version.TryParse(version,out var packaged) || packaged.ToString(3)!=release.Version.ToString(3)) throw new InvalidOperationException("Package version does not match release tag.");
        return extracted;
    }
    internal static void Install(string source) {
        var target=Path.GetDirectoryName(Environment.ProcessPath!)!;
        var probe=Path.Combine(target,".update-"+Guid.NewGuid().ToString("N")); File.WriteAllText(probe,"");File.Delete(probe);
        var helper=Path.Combine(Path.GetDirectoryName(source)!,"install.ps1");
        File.Copy(Path.Combine(AppContext.BaseDirectory,"update.ps1"),helper);
        var start=new ProcessStartInfo("powershell.exe") {UseShellExecute=false,CreateNoWindow=true};
        foreach(var arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",helper,"-Source",source,"-Target",target,"-ParentId",Environment.ProcessId.ToString()}) start.ArgumentList.Add(arg);
        using var process=Process.Start(start) ?? throw new InvalidOperationException("Cannot start updater.");
    }
}
