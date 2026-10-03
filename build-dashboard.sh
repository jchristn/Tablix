#!/bin/bash
# Build and push the Tablix dashboard image (multi-arch) with Docker Build Cloud. Equivalent to build-dashboard.bat.

set -euo pipefail

if [ $# -lt 1 ] || [ -z "${1}" ]; then
    echo "Usage: build-dashboard.sh [version-tag]"
    echo "Example: build-dashboard.sh v0.3.0"
    exit 1
fi

VERSION_TAG="${1}"
DOCKER_BUILD_CLOUD_REF="jchristn77/jchristn77"
DOCKER_BUILD_CLOUD_BUILDER="cloud-jchristn77-jchristn77"

cd "$(dirname "${BASH_SOURCE[0]}")"

echo "Building Tablix Dashboard ${VERSION_TAG} with Docker Build Cloud builder ${DOCKER_BUILD_CLOUD_BUILDER}..."
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
    -t "jchristn77/tablix-ui:${VERSION_TAG}" \
    -t jchristn77/tablix-ui:latest \
    -f dashboard/Dockerfile \
    --push \
    dashboard/; then
    echo "Tablix Dashboard build failed."
    exit 1
fi

echo "Done."
