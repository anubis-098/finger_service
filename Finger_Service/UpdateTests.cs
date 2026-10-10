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
        var folderSource=Path.Combine(root,"folder-source");Directory.CreateDirectory(folderSource);
        var names=UpdatePackage.Required.Append("fr/System.Windows.Forms.resources.dll").ToArray();
        foreach(var name in names) {var path=Path.Combine(folderSource,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,"fixture");}
        var manifest=new UpdatePackage.Manifest(1,"99.0.0",names.Select(n=>new UpdatePackage.Entry(n,new FileInfo(Path.Combine(folderSource,n)).Length,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(folderSource,n)))))).ToArray());
        File.WriteAllText(Path.Combine(folderSource,UpdatePackage.ManifestName),JsonSerializer.Serialize(manifest));
        var folderZip=Path.Combine(root,"folder.zip");
        using(var archive=ZipFile.Open(folderZip,ZipArchiveMode.Create))foreach(var name in names.Append(UpdatePackage.ManifestName))archive.CreateEntryFromFile(Path.Combine(folderSource,name),name);
        var folderExtract=Path.Combine(root,"folder-extract");Updater.Extract(folderZip,folderExtract);
        var folderTarget=Path.Combine(root,"folder-target");Directory.CreateDirectory(folderTarget);File.WriteAllText(Path.Combine(folderTarget,"settings.json"),"keep");
        UpdateInstaller.ReplaceFiles(folderExtract,folderTarget);
        if(!File.Exists(Path.Combine(folderTarget,names[^1])) || File.ReadAllText(Path.Combine(folderTarget,"settings.json"))!="keep")throw new Exception("Folder update lost resources or settings.");
        foreach(var bad in new[]{"../bad.dll","C:/bad.dll","settings.json","appsettings.json","sub/../../bad.dll","CON.dll","sub\\bad.dll"})if(UpdatePackage.SafeName(bad))throw new Exception("Unsafe manifest name accepted: "+bad);
        File.WriteAllText(Path.Combine(folderSource,names[0]),"corrupted");
        rejected=false;try{UpdatePackage.InstalledFiles(folderSource);}catch(InvalidOperationException){rejected=true;}
        if(!rejected)throw new Exception("Corrupt folder payload accepted.");
        var combined=JsonSerializer.Serialize(new {tag_name="v99.0.0",draft=false,prerelease=false,assets=new[]{"CJFingerService-win-x64.zip","CJFingerService-win-x64-folder.zip"}.Select(name=>new{name,browser_download_url="https://github.com/anubis-098/finger_service/releases/download/v99.0.0/"+name,digest="sha256:"+new string('a',64)})});
        if(Updater.ParseRelease(combined)?.Url.EndsWith("-folder.zip")!=true)throw new Exception("Folder release was not preferred.");
        var package=Environment.GetEnvironmentVariable("CJ_FINGER_TEST_PACKAGE");
        if(!string.IsNullOrWhiteSpace(package)) {
            var actual=Path.Combine(root,"actual-package");Updater.Extract(package,actual);
            if(UpdatePackage.InstalledFiles(actual).Length<UpdatePackage.Required.Length)throw new Exception("Actual folder package incomplete.");
        }
        File.WriteAllText(Path.Combine(Settings.Root,"update-test-result.txt"),"PASS: legacy/folder selection, checksums, manifest validation, path safety, nested resources, backup, settings preservation and rollback.");
    }
}
