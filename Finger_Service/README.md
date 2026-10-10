# Updating on Ubuntu / Wine

Version 1.4.7 replaces the PowerShell installer with a separate Finger Service updater process. A centered window shows overall progress: download (0-75%), SHA-256 validation (78%), extraction (80%), helper startup (84%), installation (85-97%) and restart (98-100%). The tray app only exits after the helper confirms it is ready. Updates retain a backup and restore replaced files if copying fails. Settings, queued exports and WEB8 registration are not part of the update package.

For installations running 1.4.6 or older where PowerShell cannot run under Wine, bootstrap once manually: stop Finger Service, download CJFingerService-win-x64.zip from the official release, extract its four files into the existing Finger Service program folder, replacing those four files only, and reopen with the same Wine prefix and user. Keep WEB8 files, its appsettings.json, the Wine prefix and Finger Service application data. Subsequent updates use the new installer. Ubuntu OCR bridge files are a separate package and are not changed by this update.

If installation fails, inspect `updates/<run>/install.log` under the Finger Service application-data folder. If a helper cannot start, the tray application remains open and reports the failure. An installed update opens STOPPED; click Start to resume automation.

# Skip login

In Settings > Export, enable **Skip login (WEB8 already ready)** when the registered WEB8 installation can download directly. Save the setting, then click Start when ready. Username and Password are disabled and are not required in this mode. Existing saved credentials are retained.

The macro selects the date range, downloads, waits for completion and exports TXT without clicking Login or Use Session. This applies to Windows and Wine. The option defaults to off for existing configurations. Close any open WEB8 login/error dialogs before starting. This setting does not activate WEB8 or change its Machine Key; WEB8 must already permit downloading. If downloading requires authentication, prepare WEB8 manually or disable Skip login. There is no automatic fallback to the login screen.

# CJ Finger Service 1.4.5

Ubuntu / Xfce with Wine: see [local Tesseract OCR setup](ubuntu/README.md).
Wine is detected automatically and uses native window controls plus local OCR,
bypassing Windows UI Automation (including UiaFind / FindAll). Browser login
uses detected input borders, caret checks, username OCR and password-mask count.
Full Wine compatibility still needs validation against your actual export application.

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

Login now reacquires the Username and Password selectors for each attempt. The
password field must be uniquely identified by its password role or label; the
macro no longer guesses it from field order alone. It focuses and clicks each
field, waits for stable keyboard focus, and checks the focused element before
clearing and typing every character. Username must read back exactly, including
a second check after password entry. Native password fields also have their
length checked without reading or logging the password. Embedded web password
fields that do not expose length are checked through focus and the login result.

Input verification failure, a recognized inline/modal login rejection, or login
timeout refills both fields from fresh selectors, with a maximum of three login
attempts. Persistent failure stops before selecting a session or downloading.
Only recognized login-error dialogs with an OK button are dismissed; unknown
dialogs require manual inspection. Log messages contain stages and retry counts,
never credential values. If Username cannot be verified, the macro stops instead
of submitting uncertain input. Desktop unlock and equal privilege requirements
still apply.

Save settings only remembers the export directory. The macro clicks it only when changing the directory, before downloading. A matching directory is reused without saving it again. Download retries do not re-save settings. After fresh download completion, only "3. Save TXT" creates the export file. The macro waits for a new or changed, stable TXT in the configured folder before validating or uploading it. Recognized Success confirmations after directory changes are dismissed before download. The active-task window offers Cancel and Exit.

A lost login-window click, directory confirmation, fresh-download wait and TXT export can be retried up to three attempts. Download retries use the configured timeout per attempt; export checks use 30 seconds per attempt. A download is clicked again only when no activity was observed and its button is enabled. Once activity begins, timeout retries keep waiting without restarting it, including when WEB8 leaves its buttons enabled. Export is not repeated if a new or changed file already exists. Focus activation is attempted three times. Unknown dialogs, persistently invalid credentials, locked desktops, invalid files and ambiguous multiple exports stop the task rather than being blindly acknowledged. A parent watchdog terminates a stuck worker even if a UI Automation call never returns.

Download completion accepts English or Thai ready status (including the Thai wording in the supplied WEB8 screenshot), or matching `rows loaded` and `lines prepared` counts when no status line is provided. A later busy/error status overrides earlier ready text. Mismatched counts block export. Fresh activity, a responsive window and an enabled Save TXT button are required, followed by a two-second stable completion check. Native multiline log reads use a bounded timeout with UI Automation as fallback. Every five seconds the task window and service log report elapsed time, row/prepared counts and button readiness without recording employee preview data. This handles variable loading times instead of a fixed delay.

For troubleshooting, use **Open logs** and look for `download: waiting ...`: `ready=False` means completion was not recognized; `saveEnabled=False` means the source program has not enabled export. These lines can be shared without sending employee preview contents or credentials.

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
