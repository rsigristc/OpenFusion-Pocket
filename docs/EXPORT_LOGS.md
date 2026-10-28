# Export Logs

Open the launcher's **About → Export Logs (private diagnostics)**. Android opens
its share sheet with `OpenFusion-Pocket-Logs.txt` attached. Choose where to save it
or share it manually with a bug report. The application does not upload reports.
After a crash, reopen the app and export before repeatedly restarting it: the
journal retains the current and previous session, not an unlimited history.
Include reproduction steps and what you expected in your report separately.

The report contains the app/build GUID, Unity version, Android/device model,
graphics API/GPU, memory, resolution/orientation, DS state, UTC timestamps, safe
error types and up to six managed method names. Resource probes retain fixed
categories, built-in bundle names, numeric map cells or effect IDs. Missing probes
can be normal fallback checks and are not by themselves proof of broken assets.
On Android 11+, up to three system process-exit reason/status/time/memory records
are included for this application only. Reason codes follow Android's
`ApplicationExitInfo` constants (for example 3=low memory, 4=Java crash,
5=native crash, 6=ANR, 10=user requested). A missing clean-exit marker alone does
not prove a crash. No native tombstone, process description or trace stream is read.

Privacy is enforced **before writing to disk**, rather than redacting a copy of a
raw log. Account files, passwords, access tokens, player IDs/names, chat/mail,
URLs, local paths, preferences, screenshots and packet contents are excluded.
The exporter never reads existing runtime/combat/freeze logs or Android logcat.
This deliberately sacrifices arbitrary exception message text and asset filenames
that are not on the allowlist. An unsupported feature without an error may require
reproduction steps; exporting logs does not automatically detect every bug.

The app-private journal caps each session file at 256 KiB, retains at most 192
events in a report and suppresses identical events for ten seconds. Android shares
only the generated report in a dedicated cache subdirectory through a non-exported
FileProvider with a temporary read grant. It requires no storage or READ_LOGS
permission. The export cache uses one report filename; export again to refresh it.
The existing iOS preview path shares the same sanitized report as text, and desktop
fallback writes the report to the app's persistent data directory.

Validation: `DiagnosticsRegressionChecks` covers unsafe messages/arguments/paths,
on-disk privacy, malformed retained files, restart rotation, repeated probes and
size limits. `tools/check-native-panel.ps1` also checks the installed Android
provider's private configuration, attachment readback and path confinement.
With `-RequireDiagnosticsReport`, the native check also reads the real generated
report and verifies that session-start events survive a restart. Export once,
restart the app, then export again before running that variant. It restarts the
app process; run it before logging in. This passed on the AynThor, including the
real Android share sheet, with no report sent to another service.
