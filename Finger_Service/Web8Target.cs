using System.Text.RegularExpressions;

namespace CJFingerService;

internal sealed record Web8Target(string ExecutablePath, string WorkingDirectory) {
    private static readonly Regex PrefixVariable = new("(?:^|\\s)WINEPREFIX=(?:\"(?<quoted>[^\"]+)\"|'(?<single>[^']+)'|(?<plain>[^\\s]+))", RegexOptions.Compiled);

    internal static Web8Target Resolve(string configuredPath) {
        if (string.IsNullOrWhiteSpace(configuredPath)) throw new InvalidOperationException("Choose the WEB8 executable or XFCE .desktop launcher in Settings.");
        var path = configuredPath.Trim();
        if (path.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase)) return ResolveDesktop(path);
        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new InvalidOperationException("Choose an existing WEB8 .exe or XFCE .desktop launcher in Settings.");
        return new(path, Path.GetDirectoryName(path)!);
    }

    private static Web8Target ResolveDesktop(string desktopPath) {
        if (!File.Exists(desktopPath)) throw new InvalidOperationException("The selected XFCE .desktop launcher cannot be found.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(desktopPath)) {
            if (line.StartsWith('#') || line.StartsWith('[')) continue;
            var split = line.IndexOf('=');
            if (split > 0) values[line[..split].Trim()] = UnescapeDesktop(line[(split + 1)..].Trim());
        }
        if (!values.TryGetValue("Exec", out var exec) || string.IsNullOrWhiteSpace(exec))
            throw new InvalidOperationException("The selected .desktop file has no Exec command.");

        var prefixMatch = PrefixVariable.Match(exec);
        if (prefixMatch.Success) {
            var prefix = prefixMatch.Groups["quoted"].Success ? prefixMatch.Groups["quoted"].Value :
                prefixMatch.Groups["single"].Success ? prefixMatch.Groups["single"].Value : prefixMatch.Groups["plain"].Value;
            EnsureMatchingWinePrefix(ToWinePath(prefix));
        }

        var executable = Tokenize(exec).Select(token => StripFieldCodes(token))
            .FirstOrDefault(token => token.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(executable))
            throw new InvalidOperationException("Could not find a WEB8 .exe path in the .desktop Exec command. Use a launcher whose Exec line includes the WEB8 executable.");
        executable = ToWinePath(executable);
        if (!File.Exists(executable)) throw new InvalidOperationException("WEB8 executable referenced by the .desktop launcher cannot be found: " + executable);

        var workingDirectory = values.TryGetValue("Path", out var working) && !string.IsNullOrWhiteSpace(working)
            ? ToWinePath(working) : Path.GetDirectoryName(executable)!;
        if (!Directory.Exists(workingDirectory)) workingDirectory = Path.GetDirectoryName(executable)!;
        return new(executable, workingDirectory);
    }

    private static void EnsureMatchingWinePrefix(string selectedPrefix) {
        var active = Environment.GetEnvironmentVariable("WINEPREFIX");
        if (string.IsNullOrWhiteSpace(active)) {
            var home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrWhiteSpace(home)) home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            active = Path.Combine(home, ".wine");
        }
        active = NormalizePath(ToWinePath(active));
        selectedPrefix = NormalizePath(selectedPrefix);
        if (!string.Equals(active, selectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The selected launcher uses Wine prefix '{selectedPrefix}', but Finger Service is running in '{active}'. Start Finger Service from the same XFCE/Wine prefix as WEB8, then try again.");
    }

    private static string NormalizePath(string path) {
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch { return path.TrimEnd('\\', '/'); }
    }

    private static string ToWinePath(string path) {
        path = Environment.ExpandEnvironmentVariables(path.Trim());
        if (path.StartsWith('/')) return "Z:" + path.Replace('/', '\\');
        return path;
    }

    private static string UnescapeDesktop(string value) => value.Replace("\\s", " ").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\\\", "\\");

    private static string StripFieldCodes(string token) => Regex.Replace(token, "%[fFuUdDnNickvm]", "");

    private static IEnumerable<string> Tokenize(string value) {
        var token = new System.Text.StringBuilder();
        char quote = '\0';
        var escaped = false;
        foreach (var c in value) {
            if (escaped) { token.Append(c); escaped = false; continue; }
            if (c == '\\' && quote != '\'') { escaped = true; continue; }
            if ((c == '\"' || c == '\'') && (quote == '\0' || quote == c)) { quote = quote == '\0' ? c : '\0'; continue; }
            if (char.IsWhiteSpace(c) && quote == '\0') {
                if (token.Length > 0) { yield return token.ToString(); token.Clear(); }
                continue;
            }
            token.Append(c);
        }
        if (escaped) token.Append('\\');
        if (token.Length > 0) yield return token.ToString();
    }
}
