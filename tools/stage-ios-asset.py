"""Transfer an intended-public IPA artifact without exporting private CI credentials."""
import hashlib
import json
import os
import pathlib
import re
import shutil
import urllib.parse
import urllib.request
import zipfile

url = os.environ["ARTIFACT_URL"]
print("::add-mask::" + url, flush=True)
parsed = urllib.parse.urlparse(url)
assert parsed.scheme == "https" and not parsed.username and parsed.port in (None, 443)
assert parsed.hostname and (parsed.hostname.endswith(".blob.core.windows.net") or parsed.hostname.endswith(".actions.githubusercontent.com") or parsed.hostname == "release-assets.githubusercontent.com"), "Untrusted artifact host"
work = pathlib.Path("work")
manifest = json.loads((work / "update-ios.json").read_text())
name = manifest["ipa_asset"]
assert re.fullmatch(r"OpenFusion-Pocket-iOS-v[0-9.]+\.ipa", name)
assert manifest["platform"] == "ios" and manifest["bundle"] == "com.openfusion.pocket.ios"
assert os.environ["RELEASE_TAG"] == "ios-v" + manifest["version"]
assert 0 < manifest["size"] < 1024 * 1024 * 1024
archive = work / "artifact.zip"
with urllib.request.urlopen(url, timeout=120) as response, archive.open("wb") as target:
    while chunk := response.read(1024 * 1024):
        target.write(chunk)
        assert target.tell() <= 1024 * 1024 * 1024, "Artifact too large"
with zipfile.ZipFile(archive) as zipped:
    files = [entry for entry in zipped.infolist() if entry.filename.endswith(".ipa")]
    assert len(files) == 1 and files[0].file_size == manifest["size"]
    target = work / name
    with zipped.open(files[0]) as source, target.open("wb") as output:
        shutil.copyfileobj(source, output, 1024 * 1024)
assert hashlib.sha256(target.read_bytes()).hexdigest() == manifest["sha256"], "IPA hash differs from release manifest"
print("Verified iOS release asset:", name, manifest["size"], "bytes")
