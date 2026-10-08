# Ubuntu / Xfce OCR bridge

This adds native Ubuntu Tesseract OCR to the Windows Finger Service running
under Wine. Version 1.4.4 detects Wine and bypasses Windows UI Automation entirely
for the export workflow. Native Win32 controls handle buttons, dates, editable
text and download status; local OCR handles browser-rendered text and buttons.
Credential protection, mouse/keyboard handling and the updater still depend on
Wine compatibility. Use an unlocked Xfce desktop and test with Dummy before WEB8.
Text must render correctly on screen: OCR cannot recover square missing glyphs.

## Install and start

Run these commands in Ubuntu as the desktop user:

```bash
sudo apt update
sudo apt install python3 tesseract-ocr tesseract-ocr-tha tesseract-ocr-eng fonts-thai-tlwg
python3 /path/to/Finger_Service/ubuntu/ocr_bridge.py
```

Keep this terminal open. The bridge starts no clicks or export jobs. It listens
only on `127.0.0.1:17863`, uses Thai + English, and does not send images outside
this computer. Temporary images are removed after processing. OCR requests are
limited to 10 MB and 20 seconds; CPU thread use is limited to two.

In another terminal, display the local token:

```bash
cat ~/.config/cj-finger-service/ocr-token
```

Open the updated CJFingerService.exe through the SAME Wine prefix used for WEB8.
In Settings > Export, enable **Ubuntu / Wine (local Tesseract bridge)** and paste
the token into **Local OCR token**. Click Save. The OCR language field is for the
Windows engine; Ubuntu always uses `tha+eng`. The existing server upload token
is separate. Do not paste that token into the OCR field.

The bridge token is stored locally in Wine settings; it only authorizes this
local OCR endpoint, not the attendance server. Do not expose port 17863 to LAN.

## Optional Xfce login startup

In Xfce Session and Startup > Application Autostart, add:

```text
python3 /absolute/path/to/ubuntu/ocr_bridge.py
```

This starts the OCR helper after desktop login only. Finger Service still opens
stopped and needs Start to run automation. Exit the terminal helper before
starting another copy (only one process can bind port 17863).

## Diagnose

```bash
tesseract --list-langs
python3 -m unittest discover -s /path/to/ubuntu -p 'test_*.py'
```

The language list must contain `tha` and `eng`. HTTP 401 means a token mismatch;
503 means missing/broken OCR or language files; 504 means OCR timed out. A
connection failure means the helper is stopped or is not on the same computer.
The application retains exact, unique button-caption matching; ambiguous OCR
results stop the step instead of clicking an arbitrary location.

Unit tests use simulated OCR output. A successful build or unit test does not
prove the full WEB8 workflow works under Wine; validate login, window focus,
Thai rendering, button recognition and export on the actual Ubuntu machine.

## Wine login checks (1.4.4)

The native-control workflow is also exercised against Dummy on Windows with UI
Automation bypassed. For an embedded browser, the fallback requires exactly one
Username label and one Password label, visible bordered inputs and a caret
inside the clicked field before each character is typed. Username OCR must
match exactly; the password field must show the expected number of masked
characters. Missing or ambiguous verification stops the step instead of typing
blindly. Login failures retry at most three times. If Wine's embedded browser
does not expose a caret, the program stops with a specific FOCUS_LOST message;
do not disable this check or substitute fixed coordinates.

The OCR bridge from 1.4.3 is compatible; this fix primarily replaces the .exe.
On Wine, exit Finger Service and replace the application files manually rather
than relying on the Windows PowerShell updater.

Engine reference: https://tesseract-ocr.github.io/tessdoc/Command-Line-Usage.html
