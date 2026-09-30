# CJ Finger Service 1.4.0

Compact Windows x64 tray app for WEB8 NEXT exports and attendance-server uploads. The compressed, self-contained executable includes its .NET runtime. No separate runtime installation is required.

## Start a schedule

1. Extract all four files from `CJFingerService-win-x64.zip` into a writable folder. Exit the previous app before launching the new executable.
2. In **Export**, choose the real WEB8 executable and an existing TXT export folder. Enter its username and password. The default lookback is three days through today, using Thailand dates.
3. In **Server**, enter the attendance website's base URL and service token. Create a token as Super Admin on the website's Fingerprint logs page. Use **Test connection**, then enable uploads. Remote HTTP requires explicit opt-in for a trusted test LAN; HTTPS encrypts traffic.
4. In **Schedule**, click **Start** to begin now, or choose a future date/time to start later. Times use the computer's local time. If the selected time has already arrived, clicking Start begins immediately. Runs repeat every 30 minutes afterwards.
5. Use **Stop schedule** to prevent later runs while the active run finishes. **Cancel** in the task window or tray menu stops the active worker and schedule. **Exit** confirms stopping an active worker, then terminates the tray process. The target WEB8 application is left open.

**The app always opens stopped**, including after a Windows restart or software update. Saved settings, an old scheduled time, and legacy auto-start settings never start the macro. **Save** only saves settings and stops the schedule. Closing Settings without saving preserves an already active schedule. There is no Run now shortcut. **Retry uploads** is a separate, explicit action that sends pending files without running the export macro.

The UI and documentation are English. Target-app captions remain compatible with the Thai WEB8 interface, including the Success/OK session confirmation. Those selectors are Unicode-escaped in the source.

## Progress, retries and cancellation

Every export now clicks the target Save settings button, even when its folder already matches. Recognized Success confirmations are dismissed before download. The active-task window shows the current stage and offers Cancel and Exit. Double-click the tray icon to bring it back.

A lost login-window click, directory confirmation, fresh-download wait and TXT export can be retried up to three attempts. Download retries use the configured timeout per attempt; export checks use 30 seconds per attempt. Before retrying an enabled download, the directory is saved again. A busy/disabled download button is not clicked again. Export is not repeated if a new or changed file already exists. Focus activation is attempted three times. Unknown dialogs, invalid credentials, locked desktops, invalid files and ambiguous multiple exports stop the task rather than being blindly acknowledged. A parent watchdog terminates a stuck worker even if a UI Automation call never returns.

Cancel stops the current worker and scheduled repeats. It does not delete files or undo data that the server has already received. Retry uploads remains available for retained pending files. The app does not terminate WEB8 when cancelling, timing out or exiting.

## Files and uploads

User data stays under `%LOCALAPPDATA%\CJFingerService`:

- `settings.json`: settings; passwords and tokens protected with Windows DPAPI for this Windows account.
- `pending/<run-id>/`: exported TXT, manifest with checksum/request ID, and `uploaded.json` after server acknowledgment.
- `service.log`, `worker-error.log`: operation results and diagnostic stack traces without credential values.
- `updates/<id>/`: downloaded update, installation log and previous application files.

Exports and queued files are retained. At most ten queued files are uploaded per run. Failed transfers are retried after a later successful export or with **Retry uploads**. Existing queued files are included when uploads are enabled. Keep Dummy data separate from production data. Changing the server URL does not resend files already acknowledged by a previous server.

## Updates

Source and releases: https://github.com/anubis-098/finger_service

The app checks GitHub after startup and every 12 hours. Use **Check updates** to confirm downloading and installing a newer published release. Updates are blocked during an active task. The ZIP digest and executable version must match the GitHub release. Settings and pending files stay in place. After restarting, click Start again, or choose a future time before clicking Start.

The first updater-capable release was 1.2.0. Earlier versions require one manual installation. Developer instructions are in `UPDATES.md` in the source repository.

## Requirements and limitations

Keep an interactive Windows session unlocked and run the exporter and tray app at the same privilege level. UI automation can bring the target window forward; avoid using the keyboard or mouse during a run. A Windows service in Session 0 cannot operate the desktop application.

Automation validates the expected controls and stops if focus is lost. UI Automation is preferred; the OCR fallback needs the target language installed in Windows. Some WEB8 versions may need different control selectors. Test the installed target program before unattended use.

The runtime is bundled and compressed to reduce the executable size; extracting/loading it may make the first launch slower. Only the four runtime files are shipped, with no debug symbols, source, Dummy, tests or employee data. The executable is not Authenticode-signed.

## Build and tests

Requires .NET 10 SDK on Windows:

```powershell
./Finger_Service/build.ps1
./Finger_Service/release.ps1
```

`build.ps1` writes to `publish` by default, or accepts `-OutputDirectory`. `release.ps1` creates the four-file ZIP and a SHA-256 text file under `release`.

Set `CJ_FINGER_SERVICE_DATA` to an isolated test directory before testing. `--self-test` checks schedule validation, TXT validation, encryption and update-package validation. `--automation-test` opens a temporary Dummy and tests login, session confirmation, date selection and TXT export. Set `CJ_FINGER_DEMO_NO_SESSION_POPUP=1` to test a target without the session popup. `test-update.ps1` tests installation and backup using temporary fixture files. `CJ_FINGER_DEMO_RETRY=1` makes the Dummy ignore its first download and export clicks; `CJ_FINGER_DEMO_STALL=1` simulates a stuck download for `--cancel-test` and `--exit-test`. These GUI tests use an isolated tray instance and require an isolated data directory.
