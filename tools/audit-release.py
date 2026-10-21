"""Audit downloaded CI packages before publishing the existing draft release."""
import hashlib
import io
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import struct
import subprocess
import tarfile
from zipfile import ZipFile

VERSION = "1.5.1"
ROOT = Path(__file__).resolve().parent.parent
PUBLISH = ROOT / "publish"
PUBLISH.mkdir(exist_ok=True)
expected = {f"world-speace_macOS_{arch}_v{VERSION}.{extension}" for arch in ("x64", "arm64") for extension in ("zip", "dmg")}
expected |= {f"world-speace_Linux_{arch}_v{VERSION}.{extension}" for arch in ("x64", "arm64") for extension in ("deb", "tar.gz")}
expected |= {f"world-speace_iOS_v{VERSION}-unsigned.ipa", f"world-speace_Web_v{VERSION}.zip"}
packages = {file.name: file for file in (ROOT / "downloaded").rglob("*") if file.is_file()}
assert set(packages) == expected, (set(packages) - expected, expected - set(packages))
private = re.compile(r"(^|/)(UserData|\.signing|node_modules|verification)(/|$)|\.jks$|\.p12$|keystore-password|(^|/)progress\.json$")
dictionary = (ROOT / "android/app/src/main/assets/data/words.tsv").read_bytes()


def paths_are_public(names):
    for name in names:
        assert "\\" not in name and ".." not in name.split("/") and not private.search(name), name


def asar_entry(archive, name):
    header_size = struct.unpack_from("<I", archive, 4)[0]
    json_size = struct.unpack_from("<I", archive, 12)[0]
    entry = json.loads(archive[16:16 + json_size])
    for component in name.split("/"):
        entry = entry["files"][component]
    start = 8 + header_size + int(entry["offset"])
    return archive[start:start + entry["size"]]


def desktop_assets(archive):
    assert asar_entry(archive, "www/data/words.tsv") == dictionary
    assert asar_entry(archive, "www/domain.mjs") == (ROOT / "android/app/src/main/assets/domain.mjs").read_bytes()
    assert asar_entry(archive, "main.cjs") == (ROOT / "cross-platform/desktop/app/main.cjs").read_bytes()
    assert asar_entry(archive, "www/fonts/NotoSansSC.ttf") == (ROOT / "cross-platform/desktop/fonts/NotoSansSC.ttf").read_bytes()


records = []
for name, file in sorted(packages.items()):
    if name.endswith((".zip", ".ipa")):
        with ZipFile(file) as archive:
            paths_are_public(entry.orig_filename for entry in archive.infolist())
            if "_macOS_" in name:
                desktop_assets(archive.read("world speace.app/Contents/Resources/app.asar"))
                binary = archive.read("world speace.app/Contents/MacOS/world-speace")
                assert struct.unpack_from("<I", binary, 4)[0] == (0x100000C if "_arm64_" in name else 0x1000007), name
            elif "_iOS_" in name:
                base = "Payload/WorldSpeace.app/"
                assert archive.read(base + "www/data/words.tsv") == dictionary
                info = plistlib.loads(archive.read(base + "Info.plist"))
                assert info["CFBundleShortVersionString"] == VERSION
                assert info["CFBundleVersion"] == "15002"
                assert not any("embedded.mobileprovision" in item or "/_CodeSignature/" in item for item in archive.namelist())
                binary = archive.read(base + "WorldSpeace")
                assert struct.unpack_from("<I", binary, 4)[0] == 0x100000C, "iOS device binary is not arm64"
            else:
                assert archive.read("web/data/words.tsv") == dictionary
                assert "__FILES__" not in archive.read("web/service-worker.js").decode()
    elif name.endswith((".deb", ".tar.gz")):
        if name.endswith(".deb"):
            raw = subprocess.check_output(["dpkg-deb", "--fsys-tarfile", str(file)])
            archive = tarfile.open(fileobj=io.BytesIO(raw))
        else:
            archive = tarfile.open(file)
        with archive:
            paths_are_public(item.name for item in archive.getmembers())
            members = archive.getmembers()
            app = next(item for item in members if item.name.endswith("/resources/app.asar"))
            desktop_assets(archive.extractfile(app).read())
            binary = next(item for item in members if item.isfile() and item.name.endswith("/world-speace"))
            assert binary.mode & 0o005 == 0o005, "Installed executable is not readable/executable"
            header = archive.extractfile(binary).read(64)
            assert header[:4] == b"\x7fELF" and header[4:6] == b"\x02\x01", name
            assert struct.unpack_from("<H", header, 18)[0] == (183 if "_arm64_" in name else 62), name
            for member in members:
                if member.isdir():
                    assert member.mode & 0o005 == 0o005, ("Non-traversable package directory", member.name)
    digest = hashlib.sha256(file.read_bytes()).hexdigest()
    records.append({"file": name, "bytes": file.stat().st_size, "sha256": digest})
    shutil.copyfile(file, PUBLISH / name)
    print("AUDITED", name, digest)

verification = {}
for name in ("darwin", "linux", "ios"):
    folder = ROOT / "verification" / ("verification-" + name)
    report = next(folder.glob("*.json"))
    data = json.loads(report.read_text())
    assert data.get("passed") is True if name == "ios" else data.get("count") == 8, data
    verification[name] = data

manifest = {
    "release": VERSION,
    "buildRun": os.environ["BUILD_RUN"],
    "buildCommit": json.loads((ROOT / "build-run.json").read_text())["head_sha"],
    "windowsVersion": "1.5.0",
    "androidVersion": VERSION,
    "macOSSigning": "ad hoc; no Developer ID or notarization",
    "iOSSigning": "unsigned arm64; Apple identity and provisioning required",
    "runtimeChecks": verification,
    "nativeHardwareLimits": "macOS arm64 and Linux x64 runtime checked; other desktop architectures packaged and audited; iOS Simulator checked, no physical iOS/Android device",
    "packages": records,
}
(PUBLISH / f"world-speace_v{VERSION}-platforms.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
