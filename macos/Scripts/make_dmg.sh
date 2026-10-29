#!/usr/bin/env bash
# Packages the built .app into a distributable .dmg. Runs on the macOS CI runner
# after `xcodebuild` has produced the Release .app bundle.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_PATH="${1:-${ROOT_DIR}/build/Build/Products/Release/LocalDictation.app}"
DMG_PATH="${2:-${ROOT_DIR}/LocalDictation.dmg}"
VOLUME_NAME="LocalDictation"

if [[ ! -d "${APP_PATH}" ]]; then
  echo "error: app bundle not found at ${APP_PATH}" >&2
  exit 1
fi

echo "==> Ad-hoc signing the full app bundle (deep)"
codesign --force --deep --sign - "${APP_PATH}"
codesign --verify --verbose "${APP_PATH}" || true

rm -f "${DMG_PATH}"

echo "==> Creating ${DMG_PATH}"
hdiutil create -volname "${VOLUME_NAME}" -srcfolder "${APP_PATH}" -ov -format UDZO "${DMG_PATH}"

echo "==> Done: ${DMG_PATH}"
ls -la "${DMG_PATH}"
