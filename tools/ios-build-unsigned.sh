#!/bin/bash
# Build on a Mac, then sign/install locally with a sideloading tool on Windows.
# No Apple account, provisioning profile, upload or paid service is used here.
set -euo pipefail
if [[ "$(uname -s)" != "Darwin" ]]; then echo "Building the iOS binary requires macOS and Xcode." >&2; exit 1; fi
if [[ "$#" -lt 1 || "$#" -gt 2 ]]; then
  echo "Usage: bash tools/ios-build-unsigned.sh XCODE_PROJECT_DIR [OUTPUT_DIR]" >&2; exit 1
fi
project_dir="$1"
if [[ ! -d "$project_dir/Unity-iPhone.xcodeproj" ]]; then echo "Export the Unity Xcode project first." >&2; exit 1; fi
output_dir="${2:-Builds/iOS/Unsigned}"
mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"
derived_path="$output_dir/DerivedData"
xcodebuild -project "$project_dir/Unity-iPhone.xcodeproj" -scheme Unity-iPhone \
  -configuration Release -sdk iphoneos -destination 'generic/platform=iOS' \
  -derivedDataPath "$derived_path" CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO \
  CODE_SIGN_IDENTITY= DEVELOPMENT_TEAM= build
shopt -s nullglob
apps=("$derived_path/Build/Products/Release-iphoneos/"*.app)
if [[ "${#apps[@]}" -ne 1 ]]; then echo "Expected one device application in Release-iphoneos." >&2; exit 1; fi
app="${apps[0]}"
executable="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app/Info.plist")"
if [[ ! -f "$app/$executable" ]]; then echo "Missing device executable." >&2; exit 1; fi
architectures="$(xcrun lipo -archs "$app/$executable")"
if [[ " $architectures " != *" arm64 "* ]]; then echo "Expected an ARM64 device executable, not a simulator build." >&2; exit 1; fi
package_dir="$(mktemp -d "$output_dir/package.XXXXXX")"
mkdir -p "$package_dir/Payload"
ditto "$app" "$package_dir/Payload/$(basename "$app")"
# A fresh staging directory and output name avoid mixing contents from old IPAs.
ipa_path="$output_dir/OpenFusion-Pocket-unsigned-$(date -u +%Y%m%dT%H%M%SZ)-$$.ipa"
(cd "$package_dir" && /usr/bin/zip -qry "$ipa_path" Payload)
echo "Unsigned device IPA: $ipa_path"
echo "This IPA cannot run until a sideloading tool signs it for the device. Native behavior remains unvalidated."
echo "Local packaging files retained in: $package_dir"
