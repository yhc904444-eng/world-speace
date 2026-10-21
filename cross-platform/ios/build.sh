#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
test -f WorldSpeace/www/data/words.tsv || { echo 'Run npm run prepare:assets in cross-platform first, or use the self-contained iOS source ZIP.'; exit 1; }
mkdir -p ../release
xcodebuild -project WorldSpeace.xcodeproj -scheme WorldSpeace -configuration Release -sdk iphoneos -destination 'generic/platform=iOS' -derivedDataPath ../build/ios-device CODE_SIGNING_ALLOWED=NO build
mkdir -p ../build/ios-payload/Payload
ditto ../build/ios-device/Build/Products/Release-iphoneos/WorldSpeace.app ../build/ios-payload/Payload/WorldSpeace.app
(cd ../build/ios-payload && zip -q -r ../../release/world-speace_iOS_v1.5.1-unsigned.ipa Payload)
echo 'Unsigned IPA built. Apple signing and a provisioning profile are still required for device installation.'
