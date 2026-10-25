# GitHub updater and release maintenance

The launcher checks the repository's latest **published stable GitHub release** when opened. A manual Updates button and About → Check for updates show the same release information. The changelog is the release's Markdown body, not a separate hardcoded news feed. Network failures leave login and gameplay available.

Users choose whether to download an update. Downloads stream to a private app-cache `.part` file and are checked against the declared length and SHA-256 before becoming installable. The Android bridge checks the APK hash again, verifies its application ID, requires a newer Android version code and compares its signing certificate with the installed app. Android handles installation and user confirmation. On Android 8 or newer, the user may first need to allow installation from this app in system settings, then return and tap Install update again.

No GitHub token or game-account credentials are sent to GitHub. Update metadata and initial asset URLs must point to this project's HTTPS release paths. Existing app data is retained by an ordinary Android package update with the same application ID and signing certificate.

## Publish a future update

1. Set `AppVersion` and increase `BuildCode` in `Assets/Scripts/OpenFusion/Api/PocketRelease.cs`. Project setup uses these values for Android's version name/code. Never reuse or lower a build code for a new APK.
2. Keep `com.openfusion.android` and use the same signing identity as the published APK. A locally built APK signed by another key is intentionally rejected by the updater. Back up signing keys privately; never commit them.
3. Run the release regressions, build and verify the APK's actual version, package and signing certificate.
4. Run `./tools/prepare-release.ps1`. It generates the APK, `update.json` and `SHA256SUMS.txt` in `Builds/release/` from the built file and source version constants.
5. Commit the source, create its matching `v<version>` tag and publish a stable GitHub release on this repository. Upload **all three generated assets** and write the changelog in the release notes. Mark the release as latest only when its assets are ready.
6. Check the public release API, attached manifest and APK digest. Test the update path from an older installation before announcing it broadly.

`update.json` schema 1 contains `version`, `version_code`, `package`, `apk_asset`, `size` and `sha256`. The updater cross-checks the release tag, exact asset name and URL, size and GitHub's SHA-256 asset digest when supplied. Missing or inconsistent metadata disables in-app installation and leaves a GitHub download link available.

A refreshed APK under the same release version can be detected through a higher `version_code`; normal future releases should use a new version/tag. Version comparison rejects downgrades and reinstalling an identical build.

## V1.0 refresh

The updater-enabled V1.0 uses Android build code **2**, replacing the original V1.0 build code 1. Its source, APK, checksum and update manifest are published together under the existing V1.0 release. Updating from the original installation preserves data because the application ID and signing identity remain unchanged.


## v1.5 — DS Companion

Version 1.5 uses Android build code **3**, following v1.0 build 2. The package ID and signing certificate remain unchanged. This stable release includes the DS Companion panels and gameplay/stability fixes listed in [CHANGELOG.md](../CHANGELOG.md). Its release body supplies the in-app changelog; the APK, update manifest and checksum file are published together.

## v2.0

Version 2.0 uses Android build code **4** and retains the package/signing identity. Its stable release includes the source tag, APK, `update.json` and `SHA256SUMS.txt`; the bilingual release body is used by the in-app changelog.
