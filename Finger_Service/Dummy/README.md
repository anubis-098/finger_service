# Finger Service Dummy

Windows x64 test application with a login form, date range, simulated download and TXT export. It never connects to a real fingerprint device or database. Target button captions intentionally mirror the real Thai WEB8 interface.

1. Build with `./Finger_Service/Dummy/build.ps1`, then open `publish/CJFingerDummy.exe` once to create its default export folder.
2. Set the Finger Service export program to this Dummy executable. Use `test-user` and `test-password` as test credentials.
3. Choose an existing test export folder. Keep server uploads disabled, or point them only at an isolated test server.
4. Select a future date/time in Schedule and click Start.

The Dummy requires login and session confirmation before download. It writes two synthetic TEST01 rows using the selected start and end dates. This data tests export plumbing, not payroll or shift calculations. Do not import it into a production employee database.

The default folder is `%LOCALAPPDATA%\CJFingerDummy\exports`. The macro may update this field to its configured export directory. Each export has a new filename. Keep the desktop unlocked during automation.
