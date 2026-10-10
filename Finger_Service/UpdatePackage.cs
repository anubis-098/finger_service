using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace CJFingerService;

internal static class UpdatePackage {
    internal const string ManifestName="package-manifest.json";
    internal sealed record Entry(string Path,long Size,string Sha256);
    internal sealed record Manifest(int Format,string Version,Entry[] Files);
    internal static readonly string[] Required=["CJFingerService.exe","CJFingerService.dll","CJFingerService.deps.json","CJFingerService.runtimeconfig.json","coreclr.dll","hostfxr.dll","hostpolicy.dll","ocr.ps1","update.ps1","README.md"];
    internal static bool SafeName(string name) {
        if(name.Length>220 || !Regex.IsMatch(name,@"^[A-Za-z0-9_.-]+(/[A-Za-z0-9_.-]+)*$"))return false;
        var parts=name.Split('/');
        if(parts.Any(p=>p is "." or ".." || p.EndsWith('.') || Regex.IsMatch(p,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)",RegexOptions.IgnoreCase)))return false;
        return name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase) || name=="createdump.exe" || Required.Contains(name,StringComparer.Ordinal);
    }
    internal static Manifest Read(string json) {
        var manifest=JsonSerializer.Deserialize<Manifest>(json) ?? throw new InvalidOperationException("Missing package manifest.");
        if(manifest.Format!=1 || !Version.TryParse(manifest.Version,out _) || manifest.Files is null || manifest.Files.Length>1000 || manifest.Files.Length<Required.Length ||
           manifest.Files.Any(f=>f is null || !SafeName(f.Path) || f.Size<0 || f.Size>300_000_000 || !Regex.IsMatch(f.Sha256??"",@"^[a-fA-F0-9]{64}$")) ||
           manifest.Files.Sum(f=>f.Size)>600_000_000 || manifest.Files.Select(f=>f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=manifest.Files.Length ||
           Required.Any(n=>!manifest.Files.Any(f=>f.Path==n)))throw new InvalidOperationException("Invalid folder package manifest.");
        return manifest;
    }
    internal static string[] InstalledFiles(string source) {
        var path=Path.Combine(source,ManifestName);
        if(!File.Exists(path))return Updater.Files;
        var manifest=Read(File.ReadAllText(path));
        foreach(var entry in manifest.Files) {
            var file=Path.Combine(source,entry.Path);
            if(!File.Exists(file) || new FileInfo(file).Length!=entry.Size)throw new InvalidOperationException("Package file missing or size mismatch: "+entry.Path);
            using var stream=File.OpenRead(file);
            if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(entry.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Package file checksum mismatch: "+entry.Path);
        }
        return manifest.Files.Select(f=>f.Path).Append(ManifestName).ToArray();
    }
}
