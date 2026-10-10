## Finger Service 1.4.11

- After successful local queueing/upload, confirm the matching WEB8 TXT-saved Success popup and close the same process normally. Keep Finger Service running for the next scheduled round.
- New X01/X02/X03 diagnostics; preserve WEB8 and queued files when upload fails. Refuse unknown dialogs and never force-kill WEB8 for reset.
- Use Thailand UTC+07:00 for schedule inputs, Next display, date selection and task timestamps, independent of Wine's configured timezone.
- Test schedule interpretation against UTC and test matching versus unrelated confirmation dialogs.

## Finger Service 1.4.10

- Fix D04 waiting indefinitely when loaded rows equal prepared lines but WEB8's non-empty final status is not a recognized ready word.
- Accept matching summary counts as completion evidence even with an unfamiliar final status. Still require fresh activity, responsive WEB8, Save TXT enabled and a stable ready state for two seconds.
- Loading, errors, explicit not-ready/cancelled states and mismatched counts continue to block export. Diagnostics distinguish explicit blocked states.
- Regression tests cover the reported 1145/1145 condition and incomplete/error cases; Dummy includes an unfamiliar final status.

## Finger Service 1.4.9

- Bottom-right, non-activating task status window with step IDs, elapsed times, last-update timestamp and Copy status.
- Download diagnostics identify unrecognized completion, row/count mismatch, disabled/missing Save TXT button, missing activity or an unresponsive WEB8 window.
- Distinguish Save TXT click, waiting for new file, stable/unlocked file, validation, queue and upload.
- Keep final success/failure/cancellation visible; add Show task status to the tray menu.
- Readiness, retries and export safety checks are unchanged. English UI and both Windows/Wine automation paths are supported.

Validation: build/self-tests, screenshot-verified status window and retained failure, Wine-native Dummy skip-login-to-TXT workflow on Windows. Actual Ubuntu/Wine display and real WEB8 completion still require testing on the user's machine.

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
