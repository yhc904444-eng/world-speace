#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p ../build/verification
xcodebuild -project WorldSpeace.xcodeproj -scheme WorldSpeace -configuration Debug -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' -derivedDataPath ../build/ios-simulator CODE_SIGNING_ALLOWED=NO build
device=$(xcrun simctl list devices available -j | python3 -c 'import json,sys; d=json.load(sys.stdin); print(next(x["udid"] for a in d["devices"].values() for x in a if x["name"].startswith("iPhone")))')
xcrun simctl boot "$device" || true
xcrun simctl bootstatus "$device" -b
xcrun simctl install "$device" ../build/ios-simulator/Build/Products/Debug-iphonesimulator/WorldSpeace.app
xcrun simctl launch "$device" eng.yhc904444.worldspeace --smoke-test
container=$(xcrun simctl get_app_container "$device" eng.yhc904444.worldspeace data)
for attempt in $(seq 1 90); do
  if test -f "$container/Documents/smoke-result.json"; then
    cp "$container/Documents/smoke-result.json" ../build/verification/ios-simulator.json
    xcrun simctl io "$device" screenshot ../build/verification/ios-simulator.png
    python3 -c 'import json; d=json.load(open("../build/verification/ios-simulator.json")); print(d); assert d["passed"], d'
    exit 0
  fi
  sleep 2
done
xcrun simctl io "$device" screenshot ../build/verification/ios-simulator-failed.png || true
echo 'iOS Simulator startup/persistence test timed out.' >&2
exit 1
