# CJ Finger Service

Windows x64 tray application for exporting fingerprint logs from WEB8 NEXT and uploading them to the CJ attendance server.

Download **CJFingerService-win-x64.zip** from [Releases](https://github.com/anubis-098/finger_service/releases/latest), extract all files, and run `CJFingerService.exe`.

Version 1.2.0 and later checks GitHub Releases on startup and every 12 hours. Use **Check updates** in the tray menu to download and confirm installation. Existing settings and queued files are retained.

- [Setup and usage](Finger_Service/README.md)
- [Releasing updates](Finger_Service/UPDATES.md)
- [Dummy application](Finger_Service/Dummy/README.md)

Requires an unlocked interactive Windows session. The automation must be tested against the WEB8 version installed at your site. No production credentials, employee data or logs are included.

To publish a new version, update `Finger_Service/FingerService.csproj`, commit, and push a matching tag such as `v1.2.1`. GitHub Actions builds and publishes the Windows release package.
