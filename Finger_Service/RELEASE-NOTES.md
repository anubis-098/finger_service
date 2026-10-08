## Finger Service 1.4.4

Fixes `System.NotImplementedException` in `UiaFind / AutomationElement.FindAll`
when the export macro runs under Wine on Ubuntu + Xfce.

- Wine is detected automatically. The Ubuntu option also explicitly selects the new backend.
- The export workflow bypasses Windows UI Automation, using native Win32 window controls and local OCR for browser-rendered content.
- Native login fields are verified before typing and after entry. Browser fallback detects input borders below OCR labels, checks the caret, verifies username text and counts password masking characters before submission.
- Login retries are bounded to three attempts. Unknown/ambiguous fields stop safely.
- Date selection, directory saving, download readiness and TXT validation use the Wine backend too.
- Windows retains its existing UI Automation backend.

### Upgrade

Exit Finger Service and replace the files from **CJFingerService-win-x64.zip**.
Keep the existing settings and export folders. Restart the Ubuntu OCR bridge,
then select Settings > Export > Ubuntu / Wine and retain your Local OCR token.
The 1.4.3 OCR bridge is compatible; first-time users also need
**CJFingerService-UbuntuOCR.zip** and its setup instructions.
Under Wine, install manually; the PowerShell-based Windows updater is not guaranteed to work.

### Validation and limits

- Release build and self-tests passed, including login-field detection against the provided WEB8 screenshot.
- The native backend completed the Dummy login/date/download/TXT workflow on Windows with UI Automation bypassed.
- Injected focus loss and login rejection recovered; repeated rejection stopped after three submissions without exporting.
- Actual Ubuntu + Wine + WEB8 end-to-end execution is not yet verified. Embedded-browser login requires Wine to expose a caret and correctly render the input fields; unverifiable inputs stop rather than typing blindly.
