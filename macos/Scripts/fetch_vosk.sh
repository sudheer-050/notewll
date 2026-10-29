#!/usr/bin/env bash
# Runs on the macOS CI runner only. Vendors the Vosk native lib + header + model
# into Vendor/ so project.yml's preBuildScripts sanity check and the Xcode build
# find what they expect. Never run this on Windows -- the binaries are macOS-only.
set -euo pipefail

VOSK_VERSION="0.3.45"
VOSK_API_TAG="v${VOSK_VERSION}"
MODEL_NAME="vosk-model-small-en-us-0.15"
MODEL_URL="https://alphacephei.com/vosk/models/${MODEL_NAME}.zip"
HEADER_URL="https://raw.githubusercontent.com/alphacep/vosk-api/${VOSK_API_TAG}/src/vosk_api.h"

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VENDOR_DIR="${ROOT_DIR}/Vendor"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TMP_DIR}"' EXIT

mkdir -p "${VENDOR_DIR}"

echo "==> Downloading vosk==${VOSK_VERSION} wheel"
pip3 download --no-deps -d "${TMP_DIR}/wheel" "vosk==${VOSK_VERSION}"

WHEEL_FILE="$(find "${TMP_DIR}/wheel" -name '*.whl' | head -n1)"
if [[ -z "${WHEEL_FILE}" ]]; then
  echo "error: no vosk wheel downloaded" >&2
  exit 1
fi

echo "==> Unpacking ${WHEEL_FILE}"
unzip -q "${WHEEL_FILE}" -d "${TMP_DIR}/wheel_extracted"

DYLIB_SRC="$(find "${TMP_DIR}/wheel_extracted" -name 'libvosk.dylib' | head -n1)"
if [[ -z "${DYLIB_SRC}" ]]; then
  echo "error: libvosk.dylib not found inside wheel -- wheel may not carry a macOS binary for this runner's architecture" >&2
  exit 1
fi
cp "${DYLIB_SRC}" "${VENDOR_DIR}/libvosk.dylib"

echo "==> Fixing install name + verifying arch"
install_name_tool -id "@rpath/libvosk.dylib" "${VENDOR_DIR}/libvosk.dylib"
otool -L "${VENDOR_DIR}/libvosk.dylib"
file "${VENDOR_DIR}/libvosk.dylib"

echo "==> Ad-hoc signing libvosk.dylib"
codesign --force --sign - "${VENDOR_DIR}/libvosk.dylib"

echo "==> Fetching vosk_api.h (${VOSK_API_TAG})"
curl -fsSL "${HEADER_URL}" -o "${VENDOR_DIR}/vosk_api.h"
test -s "${VENDOR_DIR}/vosk_api.h" || { echo "error: vosk_api.h download produced an empty file" >&2; exit 1; }

echo "==> Fetching ${MODEL_NAME} model"
curl -fsSL "${MODEL_URL}" -o "${TMP_DIR}/${MODEL_NAME}.zip"
unzip -q "${TMP_DIR}/${MODEL_NAME}.zip" -d "${TMP_DIR}/model_extracted"

rm -rf "${VENDOR_DIR}/${MODEL_NAME}"
mv "${TMP_DIR}/model_extracted/${MODEL_NAME}" "${VENDOR_DIR}/${MODEL_NAME}"

echo "==> Vendor contents:"
ls -la "${VENDOR_DIR}"
test -f "${VENDOR_DIR}/libvosk.dylib"
test -f "${VENDOR_DIR}/vosk_api.h"
test -d "${VENDOR_DIR}/${MODEL_NAME}/am"

echo "==> fetch_vosk.sh done"
