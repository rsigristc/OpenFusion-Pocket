"""Verify an iOS IPA and prepare platform-specific release metadata / AltStore source."""
import argparse, datetime, hashlib, json, pathlib, plistlib, re, shutil, struct, zipfile
p=argparse.ArgumentParser(description=__doc__)
p.add_argument("ipa",type=pathlib.Path);p.add_argument("--output",type=pathlib.Path,default=pathlib.Path("Builds/release-ios"));p.add_argument("--date",default=datetime.datetime.now(datetime.timezone.utc).isoformat())
a=p.parse_args();root=pathlib.Path(__file__).resolve().parents[1]
source=(root/"Assets/Scripts/OpenFusion/Api/PocketRelease.cs").read_text(encoding="utf-8")
version=re.search(r'const string IosAppVersion = "([0-9.]+)"',source)[1]
build=re.search(r'const int IosBuildCode = (\d+)',source)[1]
bundle=re.search(r'const string IosBundle = "([^"]+)"',source)[1]
repo="https://github.com/rsigristc/OpenFusion-Pocket";tag="ios-v"+version
with zipfile.ZipFile(a.ipa) as z:
 ip=next(n for n in z.namelist() if n.startswith("Payload/") and n.count("/")==2 and n.endswith("/Info.plist"))
 app=ip.rsplit("/",1)[0];info=plistlib.loads(z.read(ip))
 assert info["CFBundleIdentifier"]==bundle and info["CFBundleShortVersionString"]==version and info["CFBundleVersion"]==build,"IPA version / bundle differs from source"
 exe=z.read(app+"/"+info["CFBundleExecutable"])
 for data in [exe,z.read(app+"/Frameworks/UnityFramework.framework/UnityFramework")]:
  assert struct.unpack_from("<II",data)==(0xfeedfacf,0x100000c),"IPA must be native ARM64"
 assert not any(any(x in n.lower() for x in ["qemu","husk","angle.framework"]) for n in z.namelist()),"Unexpected translation-layer binaries"
 entitlements={};offset=32
 for _ in range(struct.unpack_from("<I",exe,16)[0]):
  cmd,command_length=struct.unpack_from("<II",exe,offset)
  if cmd==0x1d:
   start,size=struct.unpack_from("<II",exe,offset+8);blob=exe[start:start+size]
   magic,total,count=struct.unpack_from(">III",blob);assert magic==0xfade0cc0
   for i in range(count):
    slot,at=struct.unpack_from(">II",blob,12+i*8)
    if slot==5:
     _,entitlement_length=struct.unpack_from(">II",blob,at);entitlements=plistlib.loads(blob[at+8:at+entitlement_length])
  offset+=command_length
 assert entitlements.get("get-task-allow") is True,"Expected lab signature permissions changed; inspect before publishing"
 minimum=info["MinimumOSVersion"]
a.output.mkdir(parents=True,exist_ok=True);name=f"OpenFusion-Pocket-iOS-v{version}.ipa";target=a.output/name
if a.ipa.resolve()!=target.resolve():shutil.copyfile(a.ipa,target)
sha=hashlib.sha256(target.read_bytes()).hexdigest();size=target.stat().st_size
manifest={"schema":1,"platform":"ios","bundle":bundle,"version":version,"version_code":int(build),"minimum_os":minimum,"ipa_asset":name,"size":size,"sha256":sha}
(a.output/"update-ios.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
ms=hashlib.sha256((a.output/"update-ios.json").read_bytes()).hexdigest()
(a.output/"SHA256SUMS.txt").write_text(f"{sha}  {name}\n{ms}  update-ios.json\n",encoding="utf-8")
source_path=root/"altstore.json"
previous=json.loads(source_path.read_text(encoding="utf-8")) if source_path.exists() else {"apps":[]}
old=next((app.get("versions",[]) for app in previous.get("apps",[]) if app.get("bundleIdentifier")==bundle),[])
entry={"version":version,"buildVersion":build,"date":a.date,"localizedDescription":(root/f"docs/releases/{tag}.md").read_text(encoding="utf-8"),"downloadURL":repo+f"/releases/download/{tag}/"+name,"size":size,"minOSVersion":minimum}
versions=[entry]+[v for v in old if (v.get("version"),v.get("buildVersion"))!=(version,build)]
catalog={"name":"OpenFusion:Pocket iOS","website":repo,"tintColor":"00BCD4","apps":[{"name":"OpenFusion:Pocket","bundleIdentifier":bundle,"developerName":"Rodrigo Sigrist / OpenFusion:Pocket community","localizedDescription":"Experimental native FusionFall client for iOS. Single-screen touch controls, ARM64 and Metal. No external JIT. See the repository for credits and limitations.","iconURL":"https://raw.githubusercontent.com/rsigristc/OpenFusion-Pocket/main/Assets/Art/AppIcon/FusionSpawnIcon-v1.png","category":"games","versions":versions,"appPermissions":{"entitlements":sorted(entitlements),"privacy":{k:v for k,v in info.items() if k.endswith("UsageDescription")}}}],"news":[]}
source_path.write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(json.dumps(manifest));print("AltStore source generated from actual IPA version and permissions.")
