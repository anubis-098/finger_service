# WEB8 WebView2 launcher for Ubuntu + Wine

The launcher sets `WEBVIEW2_USER_DATA_FOLDER` to a writable, per-executable directory, changes the working directory to the WEB8 executable folder (so relative config files can be found), records WEB8/Wine output, and prevents duplicate launches through this launcher from using the same profile. It does not modify WEB8 or its bundled WebView2 assemblies.

## Run it

Close all WEB8 windows first. From the same Ubuntu user and Wine prefix that contain the installed WEB8 app and WebView2 Runtime, open a terminal inside the logged-in Xfce desktop session and run:

```bash
cd /path/to/Finger_Service/ubuntu
chmod +x web8-webview2-launcher.sh
WINEPREFIX="$HOME/.wine" ./web8-webview2-launcher.sh \
  "$HOME/path/to/Web8NextAttendanceClient.exe"
```

Use the actual executable path. If WEB8 is installed in another Wine prefix, replace `$HOME/.wine` with that prefix. Do not run this as `root`, from a headless system service, or from a different user session; the app needs the logged-in desktop and the prefix where its runtime is installed.

The default WebView2 profile is stored under:

```text
~/.local/share/web8-webview2/<executable-name>/
```

The launcher does not delete profile data or terminate other processes. Logs are written under:

```text
~/.local/state/web8-webview2/
```

## If the error remains

1. Close WEB8 and its WebView2 child processes normally, then start it only through this launcher.
2. Check that the profile and log directories are writable by the Ubuntu user.
3. Confirm WEB8, Wine, and the WebView2 Runtime are all using the same `WINEPREFIX`.
4. Attach the newest launcher log and the exact WEB8 executable name when reporting the result.

If `0x800700AA` still occurs with the isolated profile, the cause is likely outside profile contention (for example, the WebView2 COM/controller implementation under Wine). This helper cannot replace the native WebView2 controller expected by WEB8. Microsoft documents WebView2 support for Windows, Xbox, and HoloLens, not Wine/Linux; the reliable fallback is running WEB8 in Windows or a Windows VM and sending its exported TXT to the existing Finger Service receiver.
