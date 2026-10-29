"""Compile iOS C# branches with Unity's Roslyn and existing player references.

This does not compile Objective-C++, validate the iOS SDK, run IL2CPP for iOS,
produce an Xcode project or prove device compatibility. Run after a Unity build
has refreshed its player response file. No project settings are changed.
"""
import argparse
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--unity-editor", required=True, type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
editor = args.unity_editor
candidates = list((root / "Library/Bee/artifacts").glob("*/Assembly-CSharp.rsp"))
if not candidates:
    raise SystemExit("Run a Unity player build first to create the current compiler response file.")
source = max(candidates, key=lambda path: path.stat().st_mtime)
lines = source.read_text(encoding="utf-8-sig").splitlines()
out = root / "Logs/ios/managed-check"
out.mkdir(parents=True, exist_ok=True)
filtered = []
for line in lines:
    if line.startswith(("-out:", "-refout:")):
        continue
    if line.startswith("-define:") and any(token in line for token in ("ANDROID", "UNITY_EDITOR", "UNITY_STANDALONE", "PLATFORM_STANDALONE", "POCKET_INPUT_PREVIEW")):
        continue
    if "UnityEngine.AndroidJNIModule.dll" in line:
        continue
    filtered.append(line)
filtered += ["-define:UNITY_IOS", "-define:UNITY_IPHONE", "-define:PLATFORM_IOS", '-out:"' + (out / "Pocket-iOS-source-check.dll").as_posix() + '"']
response = out / "ios.rsp"
response.write_text("\n".join(filtered) + "\n", encoding="utf-8")
command = [str(editor / "Data/NetCoreRuntime/dotnet.exe"), str(editor / "Data/DotNetSdkRoslyn/csc.dll"), "@" + str(response)]
result = subprocess.run(command, cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
(out / "compiler.log").write_text(result.stdout, encoding="utf-8")
print(result.stdout[-5000:])
print("iOS C# source check:", "PASS" if result.returncode == 0 else "FAIL", "(not an iOS SDK/native build)")
raise SystemExit(result.returncode)
