## Finger Service 1.4.8

- New recommended CJFingerService-win-x64-folder.zip: small EXE with runtime/DLLs alongside it. No single-file runtime decompression at startup; .NET installation is still unnecessary.
- Limit runtime satellite resources to English. No claim of measured steady-state RAM/CPU reduction.
- Updater prefers folder packages and validates manifest paths, sizes and per-file SHA-256, with multi-file backups and rollback. Keep the centered updater/progress window.
- Retain CJFingerService-win-x64.zip as the four-file legacy distribution for older updaters. Users switching now must extract the full folder ZIP once; later updates use folder packages automatically.
- Preserve settings, queued exports, Wine prefix and WEB8 registration.

Validation: build and unit tests; packaged Windows integration verified the updater window, handshake, replacement, backup and restart with the multi-file runtime. Ubuntu/Wine requires validation on the user's machine.

## Finger Service 1.4.7

- Replace PowerShell-based installation with a separate executable updater for Windows and Wine.
- Add centered update windows with overall percentage, SHA-256 verification, extraction, installation and restart status.
- Wait for a helper-ready handshake before closing the tray app. Preserve backups and roll back file-copy failures.
- Keep the strict four-file package allowlist, checksum checks, settings and queued exports.
- Older Wine installations with a broken PowerShell updater need one manual ZIP replacement to reach this version.

Validation: build and self-tests include successful replacement, backup preservation, untouched settings and rollback after a partial-copy failure. A packaged Windows integration test verified the dedicated window/handle, ready handshake, parent exit, installation and restart. Actual Ubuntu/Wine installation still requires verification on the user's machine.

## Finger Service 1.4.6

- Add Settings > Export > Skip login (WEB8 already ready), saved per installation and off by default.
- Skip Login, credential entry, Use Session and its confirmation on both Windows and Wine. Continue with dates, download readiness and TXT export.
- Disable credential inputs and skip credential validation/decryption in this mode, preserving stored credentials.
- Stop clearly if a login or modal dialog is still open. No automatic login fallback and no changes to WEB8 licensing.
- Regression fixture checks that no login dialog opens, settings survive save/load, missing credentials are accepted only in skip mode, dates match and a validated TXT is produced.

Validation: build and self-tests; Wine-native backend Dummy export on Windows passed with skip login. Initial hidden-launch test could not acquire foreground focus; direct-launch retry passed. Actual Ubuntu/Wine WEB8 remains to be tested on the licensed machine.

## Finger Service 1.4.5

Corrects the distinction between saving the export directory and exporting scan data.

- Save settings is clicked only when the configured export directory changes, before downloading.
- An existing matching directory is reused without saving it again.
- Download retries no longer click Save settings.
- After fresh download completion, "3. Save TXT" creates the file. The macro waits for a new or changed, stable TXT before validating and uploading it.
- Applies to both Windows and Wine automation. Dummy now models directory persistence separately from download and export.

Validation: build passed. The Wine native backend completed Dummy workflows on Windows with both unchanged and changed directories, checking zero and one directory-save clicks respectively. The Windows UI Automation test could not find the fixture main window in this environment; its end-to-end validation remains incomplete. Real WEB8 on Ubuntu has not been validated end to end.

Download CJFingerService-win-x64.zip and replace application files after exiting the app. Existing settings and exports remain in their separate data folder. Ubuntu users can keep the existing OCR bridge. First-time Ubuntu users also need CJFingerService-UbuntuOCR.zip and its setup guide. Install manually under Wine; the Windows PowerShell updater is not guaranteed to work there.
