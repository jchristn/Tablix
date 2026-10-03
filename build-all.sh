#!/bin/bash
# Build and push the Tablix dashboard and server images in order. Equivalent to build-all.bat.

set -euo pipefail

if [ $# -lt 1 ] || [ -z "${1}" ]; then
    echo "Usage: build-all.sh [version-tag]"
    echo "Example: build-all.sh v0.3.0"
    exit 1
fi

VERSION_TAG="${1}"
DOCKER_BUILD_CLOUD_BUILDER="cloud-jchristn77-jchristn77"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Using Docker Build Cloud builder ${DOCKER_BUILD_CLOUD_BUILDER}."

"${SCRIPT_DIR}/build-dashboard.sh" "${VERSION_TAG}"
"${SCRIPT_DIR}/build-server.sh" "${VERSION_TAG}"

echo "Done."
