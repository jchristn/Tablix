#!/bin/bash
# Build and push the Tablix server image (multi-arch) with Docker Build Cloud. Equivalent to build-server.bat.

set -euo pipefail

if [ $# -lt 1 ] || [ -z "${1}" ]; then
    echo "Usage: build-server.sh [version-tag]"
    echo "Example: build-server.sh v0.3.0"
    exit 1
fi

VERSION_TAG="${1}"
DOCKER_BUILD_CLOUD_REF="jchristn77/jchristn77"
DOCKER_BUILD_CLOUD_BUILDER="cloud-jchristn77-jchristn77"

cd "$(dirname "${BASH_SOURCE[0]}")"

echo "Building Tablix Server ${VERSION_TAG} with Docker Build Cloud builder ${DOCKER_BUILD_CLOUD_BUILDER}..."
if ! docker buildx inspect "${DOCKER_BUILD_CLOUD_BUILDER}" >/dev/null 2>&1; then
    echo "Connecting Docker Build Cloud builder ${DOCKER_BUILD_CLOUD_REF}..."
    if ! docker buildx create --driver cloud "${DOCKER_BUILD_CLOUD_REF}" >/dev/null; then
        echo "Failed to connect Docker Build Cloud builder ${DOCKER_BUILD_CLOUD_REF}."
        exit 1
    fi
fi

if ! docker buildx build \
    --builder "${DOCKER_BUILD_CLOUD_BUILDER}" \
    --platform linux/amd64,linux/arm64/v8 \
    -t "jchristn77/tablix-server:${VERSION_TAG}" \
    -t jchristn77/tablix-server:latest \
    -f src/Tablix.Server/Dockerfile \
    --push \
    src/; then
    echo "Tablix Server build failed."
    exit 1
fi

echo "Done."
