using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CJFingerService;
public sealed class Settings {
    public string ProgramPath { get; set; } = "";
    public string ServerUrl { get; set; } = "";
    public bool EnableUpload { get; set; }
    public bool AllowHttp { get; set; }
    public string ExportDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    public string Username { get; set; } = "getdata";
    public string PasswordProtected { get; set; } = "";
    public string TokenProtected { get; set; } = "";
    public int LookbackDays { get; set; } = 3;
    public int DownloadTimeoutSeconds { get; set; } = 600;
    public DateTime? ScheduleStartAt { get; set; }
    public string OcrLanguage { get; set; } = "th";
    public bool UseUbuntuOcr { get; set; }
    public string OcrBridgeToken { get; set; } = "";
    public static readonly string Root = Environment.GetEnvironmentVariable("CJ_FINGER_SERVICE_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CJFingerService");
    public static readonly string ConfigPath = Path.Combine(Root, "settings.json");
    public static string Protect(string value) => value.Length == 0 ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    public static string Unprotect(string value) => value.Length == 0 ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser));
    public static Settings Load() { Directory.CreateDirectory(Root); return File.Exists(ConfigPath) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(ConfigPath)) ?? new() : new(); }
    public void Save() { Directory.CreateDirectory(Root); var temp = ConfigPath + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, ConfigPath, true); }
    public void Validate(bool upload = true) {
        if (!File.Exists(ProgramPath) || !ProgramPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose the WEB8 NEXT executable in Settings.");
        if (!Directory.Exists(ExportDirectory)) throw new InvalidOperationException("Export directory does not exist.");
        if (string.IsNullOrWhiteSpace(Username) || Unprotect(PasswordProtected).Length == 0) throw new InvalidOperationException("Set the WEB8 username and password.");
        if (LookbackDays < 0 || LookbackDays > 31) throw new InvalidOperationException("Lookback must be 0–31 days.");
        if (EnableUpload) _ = UploadClient.BaseUri(this);
        if (upload && (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))) throw new InvalidOperationException("Use HTTPS for remote servers (HTTP is allowed only on localhost).");
        if (upload && Unprotect(TokenProtected).Length < 32) throw new InvalidOperationException("Create a Service token on the Fingerprint logs page and paste it in Settings.");
    }
}
