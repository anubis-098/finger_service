using System.IO.Compression;
using System.Text.Json;
namespace CJFingerService;
internal static class UpdateTests {
    internal static void Run() {
        string Release(string tag,bool prerelease=false,string? digest=null)=>JsonSerializer.Serialize(new {tag_name=tag,draft=false,prerelease,assets=new[]{new {name="CJFingerService-win-x64.zip",browser_download_url="https://github.com/anubis-098/finger_service/releases/download/"+tag+"/CJFingerService-win-x64.zip",digest=digest??"sha256:"+new string('a',64)}}});
        if(Updater.ParseRelease(Release("v99.0.0")) is null || Updater.ParseRelease(Release("v1.0.0")) is not null || Updater.ParseRelease(Release("v99.0.0",true)) is not null) throw new Exception("Update version test failed.");
        var rejected=false;try {Updater.ParseRelease(Release("v99.0.0",digest:"invalid"));}catch(InvalidOperationException){rejected=true;}
        if(!rejected) throw new Exception("Missing digest accepted.");
        var root=Path.Combine(Settings.Root,"update-test",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var zip=Path.Combine(root,"good.zip");
        using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)) foreach(var file in Updater.Files) {using var writer=new StreamWriter(archive.CreateEntry(file).Open());writer.Write("fixture");}
        Updater.Extract(zip,Path.Combine(root,"good"));
        zip=Path.Combine(root,"bad.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)) {archive.CreateEntry("../outside.exe");}
        rejected=false;try{Updater.Extract(zip,Path.Combine(root,"bad"));}catch(InvalidOperationException){rejected=true;}
        if(!rejected || File.Exists(Path.Combine(root,"outside.exe"))) throw new Exception("Unsafe archive accepted.");
        var installRoot=Path.Combine(root,"install");
        var staged=Path.Combine(installRoot,"files");var target=Path.Combine(root,"target");
        Directory.CreateDirectory(staged);Directory.CreateDirectory(target);
        foreach(var name in Updater.Files) {File.WriteAllText(Path.Combine(staged,name),"new");File.WriteAllText(Path.Combine(target,name),"old");}
        File.WriteAllText(Path.Combine(target,"settings.json"),"keep-settings");
        UpdateInstaller.ReplaceFiles(staged,target);
        if(Updater.Files.Any(name=>File.ReadAllText(Path.Combine(target,name))!="new" || File.ReadAllText(Path.Combine(installRoot,"backup",name))!="old") || File.ReadAllText(Path.Combine(target,"settings.json"))!="keep-settings")throw new Exception("Update replacement or backup failed.");
        var failureRoot=Path.Combine(root,"failure");staged=Path.Combine(failureRoot,"files");target=Path.Combine(failureRoot,"target");
        Directory.CreateDirectory(staged);Directory.CreateDirectory(target);
        foreach(var name in Updater.Files)File.WriteAllText(Path.Combine(staged,name),"new");
        File.WriteAllText(Path.Combine(target,Updater.Files[0]),"old");
        Directory.CreateDirectory(Path.Combine(target,Updater.Files[1])); // Fail after the EXE was replaced.
        rejected=false;try {UpdateInstaller.ReplaceFiles(staged,target);}catch(IOException){rejected=true;}
        if(!rejected || File.ReadAllText(Path.Combine(target,Updater.Files[0]))!="old")throw new Exception("Failed update did not restore original executable.");
        File.WriteAllText(Path.Combine(Settings.Root,"update-test-result.txt"),"PASS: version checks, SHA-256 requirement, allowlist, traversal rejection, installation, backups, settings preservation and partial-copy rollback.");
    }
}
