# CJ Finger Service

Windows x64 tray application for exporting fingerprint logs from WEB8 NEXT and uploading them to the CJ attendance server.

Download **CJFingerService-win-x64.zip** from [Releases](https://github.com/anubis-098/finger_service/releases/latest), extract all files, and run `CJFingerService.exe`.

Version 1.2.0 and later checks GitHub Releases on startup and every 12 hours. Use **Check updates** in the tray menu to download and confirm installation. Existing settings and queued files are retained.

Version 1.3 has an English-only, tabbed interface and a compressed standalone executable. It always opens **stopped**, including after an update. From 1.3.1, click **Start** to begin now or select a future date/time to start later. Runs repeat every 30 minutes. **Save** does not start automation.

Version 1.4.0 always saves the export directory before downloading, retries stalled download/export steps up to three attempts, and adds an active-task window with Cancel and Exit. Cancelling stops the worker and schedule; exiting closes the tray process while leaving WEB8 open.

- [Setup and usage](Finger_Service/README.md)
Version 1.4.1 recognizes Thai and English download completion and matching loaded/prepared counts, waits for stable readiness before saving, and reports progress every five seconds. Downloads already in progress are not restarted. Tested with delayed fixtures; validate against your installed WEB8 version.

- [Setup and usage](Finger_Service/README.md)
- [Releasing updates](Finger_Service/UPDATES.md)
- [Dummy application](Finger_Service/Dummy/README.md)

Requires an unlocked interactive Windows session. The automation must be tested against the WEB8 version installed at your site. No production credentials, employee data or logs are included.

To publish a new version, update `Finger_Service/FingerService.csproj`, commit, and push a matching tag such as `v1.2.1`. GitHub Actions builds and publishes the Windows release package.
