# GitHub release maintenance

Repository: https://github.com/anubis-098/finger_service

1. Update `<Version>` in `FingerService.csproj`, using three numeric parts.
2. Build and test the release. Keep the repository's `Finger_Service` source directory synchronized. Never commit local settings, tokens, pending exports or logs.
3. Commit the changes and push a matching tag, such as `v1.3.0`.
4. `.github/workflows/release.yml` checks the tag, builds on Windows and publishes the ZIP using the GitHub Actions token.
5. Verify the latest release API reports the expected tag and a SHA-256 digest for the ZIP.

For manual publication, run `Finger_Service/release.ps1` and attach `release/CJFingerService-win-x64.zip` plus `SHA256.txt` to a published, non-prerelease GitHub Release. Do not rename the ZIP. It must contain exactly `CJFingerService.exe`, `ocr.ps1`, `update.ps1` and `README.md` at the root.

Clients validate the ZIP digest against GitHub's asset metadata and compare the executable version with the release tag. GitHub checksum validation is not an Authenticode signature. A release with a missing digest or unexpected package files is rejected.

Clients check on startup and every 12 hours, and install only after confirmation. The helper waits for the old process to exit, backs up application files and installs the replacement. It attempts rollback if file installation fails. It does not detect every crash or behavioral regression after the new executable starts. Installation logs and backups remain under `%LOCALAPPDATA%\CJFingerService\updates`.

Version 1.3.0 always restarts stopped. The user must click Start again after updating. From 1.3.1, the current/past selected time starts immediately on that click; a future time schedules a later start. User settings and queued exports are not replaced by an update.

API reference: https://docs.github.com/en/rest/releases/releases#get-the-latest-release
