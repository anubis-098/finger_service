## Finger Service 1.4.5

Corrects the distinction between saving the export directory and exporting scan data.

- Save settings is clicked only when the configured export directory changes, before downloading.
- An existing matching directory is reused without saving it again.
- Download retries no longer click Save settings.
- After fresh download completion, "3. Save TXT" creates the file. The macro waits for a new or changed, stable TXT before validating and uploading it.
- Applies to both Windows and Wine automation. Dummy now models directory persistence separately from download and export.

Validation: build passed. The Wine native backend completed Dummy workflows on Windows with both unchanged and changed directories, checking zero and one directory-save clicks respectively. The Windows UI Automation test could not find the fixture main window in this environment; its end-to-end validation remains incomplete. Real WEB8 on Ubuntu has not been validated end to end.

Download CJFingerService-win-x64.zip and replace application files after exiting the app. Existing settings and exports remain in their separate data folder. Ubuntu users can keep the existing OCR bridge. First-time Ubuntu users also need CJFingerService-UbuntuOCR.zip and its setup guide. Install manually under Wine; the Windows PowerShell updater is not guaranteed to work there.
