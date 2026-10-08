## Finger Service 1.4.3

Adds optional native Ubuntu Tesseract OCR for Finger Service running under Wine.

- Download **CJFingerService-win-x64.zip** for the application (also used by the existing Windows updater).
- Ubuntu / Xfce users must additionally download and extract **CJFingerService-UbuntuOCR.zip**, then follow `ubuntu/README.md`.
- Install Python 3, Tesseract and Thai/English language data; start the local bridge and paste its token into Settings > Export > Local OCR token. Enable Ubuntu / Wine OCR.
- Screenshots are processed locally on 127.0.0.1; requests require a token and have image-size/time limits.
- The application still opens stopped. Existing Windows OCR remains the default.

This changes the OCR engine only. Full WEB8 automation, UI Automation, login, and window focus under Wine still require testing on the target Ubuntu machine. Thai text must render correctly before OCR can read it. The Windows PowerShell updater is not guaranteed to run under Wine; Ubuntu users should replace the application files manually after exiting it.

Validation: Release build and Python bridge unit tests passed. No end-to-end WEB8-on-Ubuntu validation has been completed.
