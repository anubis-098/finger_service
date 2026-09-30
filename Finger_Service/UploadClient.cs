using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
namespace CJFingerService;
internal static class UploadClient {
    internal static Uri BaseUri(Settings settings) {
        if(!Uri.TryCreate(settings.ServerUrl.Trim().TrimEnd('/')+"/",UriKind.Absolute,out var uri) || (uri.Scheme!="https" && uri.Scheme!="http") || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0) throw new InvalidOperationException("Enter the website URL, e.g. https://attendance.example.com (no login credentials or query).");
        if(uri.Scheme=="http" && !uri.IsLoopback && !settings.AllowHttp) throw new InvalidOperationException("Use HTTPS, or explicitly enable HTTP for your trusted test LAN.");
        if(Settings.Unprotect(settings.TokenProtected).Length<32) throw new InvalidOperationException("Enter a Service token from the website Fingerprint logs page.");
        return uri;
    }
    private static HttpClient Client(Settings settings) {
        var client=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false }) { BaseAddress=BaseUri(settings),Timeout=TimeSpan.FromSeconds(45) };
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",Settings.Unprotect(settings.TokenProtected));
        client.DefaultRequestHeaders.Add("X-Machine-Name",Environment.MachineName);
        client.DefaultRequestHeaders.Add("X-Service-Version",Updater.VersionText);
        return client;
    }
    private static async Task<string> ReadResponse(HttpResponseMessage response) {
        var text=await response.Content.ReadAsStringAsync();
        if(!response.IsSuccessStatusCode) {
            var reason=response.StatusCode switch {System.Net.HttpStatusCode.Unauthorized=>"Invalid or revoked Service token",System.Net.HttpStatusCode.NotFound=>"Server receiver is not installed at this URL",_=>"Server rejected the request"};
            throw new InvalidOperationException($"{reason} (HTTP {(int)response.StatusCode}). File kept locally.");
        }
        using var json=JsonDocument.Parse(text);
        if(!json.RootElement.TryGetProperty("ok",out var ok)||!ok.GetBoolean()) throw new InvalidOperationException("Unexpected server response. File kept locally.");
        return text;
    }
    internal static async Task Test(Settings settings) {
        using var client=Client(settings);
        using var response=await client.PostAsync("api/fingerprint-service/heartbeat",null);
        await ReadResponse(response);
    }
    internal static async Task<int> SendPending(Settings settings) {
        using var client=Client(settings);
        var root=Path.Combine(Settings.Root,"pending");
        if(!Directory.Exists(root)) return 0;
        var count=0;
        foreach(var directory in Directory.GetDirectories(root).OrderBy(p=>p)) {
            if(File.Exists(Path.Combine(directory,"uploaded.json"))) continue;
            var manifest=Path.Combine(directory,"manifest.json");
            if(!File.Exists(manifest)) continue;
            using var json=JsonDocument.Parse(await File.ReadAllTextAsync(manifest));
            var filename=json.RootElement.GetProperty("filename").GetString()!;
            if(Path.GetFileName(filename)!=filename) throw new InvalidOperationException("Invalid queued filename.");
            var path=Path.Combine(directory,filename); ExportAutomation.ValidateFile(path);
            var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(path)));
            if(!string.Equals(hash,json.RootElement.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Queued file changed after export. Upload stopped.");
            var id=Guid.Parse(json.RootElement.GetProperty("runId").GetString()!);
            using var request=new HttpRequestMessage(HttpMethod.Post,"api/fingerprint-service/upload");
            request.Headers.Add("X-Request-ID",id.ToString()); request.Headers.Add("X-Filename",Uri.EscapeDataString(filename));
            request.Content=new ByteArrayContent(await File.ReadAllBytesAsync(path)); request.Content.Headers.ContentType=new MediaTypeHeaderValue("text/plain");
            using var response=await client.SendAsync(request);
            var receipt=await ReadResponse(response);
            var acknowledgment=JsonSerializer.Serialize(new {server=client.BaseAddress!.ToString(),uploadedAt=DateTimeOffset.Now,response=JsonSerializer.Deserialize<JsonElement>(receipt)});
            await File.WriteAllTextAsync(Path.Combine(directory,"uploaded.tmp"),acknowledgment);
            File.Move(Path.Combine(directory,"uploaded.tmp"),Path.Combine(directory,"uploaded.json"),true);
            count++;
            if(count>=10) break; // Bound each worker run; retain remaining files for the next cycle.
        }
        return count;
    }
}
