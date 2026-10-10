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
