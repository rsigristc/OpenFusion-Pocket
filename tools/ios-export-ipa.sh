#!/bin/bash
# Run on macOS after Unity exported Builds/iOS/Xcode. No uploads are performed.
set -euo pipefail
if [[ "$(uname -s)" != "Darwin" ]]; then echo "This step requires macOS and Xcode." >&2; exit 1; fi
if [[ "$#" -lt 2 || "$#" -gt 3 ]]; then
  echo "Usage: bash tools/ios-export-ipa.sh XCODE_PROJECT_DIR EXPORT_OPTIONS_PLIST [OUTPUT_DIR]" >&2; exit 1
fi
project_dir="$1"
options_plist="$2"
output_dir="${3:-Builds/iOS/Signed}"
if [[ ! -d "$project_dir/Unity-iPhone.xcodeproj" || ! -f "$options_plist" ]]; then echo "Missing Xcode project or signing export options." >&2; exit 1; fi
mkdir -p "$output_dir"
archive_path="$output_dir/OpenFusion-Pocket.xcarchive"
signing_args=()
if [[ -n "${POCKET_APPLE_TEAM_ID:-}" ]]; then signing_args+=("DEVELOPMENT_TEAM=$POCKET_APPLE_TEAM_ID"); fi
xcodebuild -project "$project_dir/Unity-iPhone.xcodeproj" -scheme Unity-iPhone \
  -configuration Release -destination 'generic/platform=iOS' -archivePath "$archive_path" \
  "${signing_args[@]}" archive
xcodebuild -exportArchive -archivePath "$archive_path" -exportOptionsPlist "$options_plist" -exportPath "$output_dir"
echo "IPA export finished in $output_dir. Device installation and runtime validation remain separate steps."
