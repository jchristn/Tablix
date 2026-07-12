#!/bin/bash

set -euo pipefail

factory_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
docker_dir="$(cd "${factory_dir}/.." && pwd)"

echo ""
echo "============================================"
echo "  Tablix Factory Reset"
echo "============================================"
echo ""
echo "WARNING: This will reset Tablix to factory defaults."
echo "All data, configuration changes, and logs will be lost."
echo ""
read -p "Are you sure? (y/N): " confirm
if [ "$confirm" != "y" ] && [ "$confirm" != "Y" ]; then
    echo "Cancelled."
    exit 0
fi

echo ""
echo "Stopping containers..."
if ! docker compose -f "${docker_dir}/compose.yaml" down; then
    echo "Warning: docker compose down failed; continuing with local factory reset."
fi

echo ""
echo "Restoring factory database..."
cp -f "${factory_dir}/database.db" "${docker_dir}/database.db"
if [ -d "${docker_dir}/tablix.db" ]; then
    rm -rf "${docker_dir}/tablix.db"
fi
cp -f "${factory_dir}/tablix.db" "${docker_dir}/tablix.db"
rm -f "${docker_dir}/tablix.db-wal" "${docker_dir}/tablix.db-shm"

echo ""
echo "Restoring factory configuration..."
cp -f "${factory_dir}/tablix.json" "${docker_dir}/tablix.json"

echo ""
echo "Clearing logs..."
rm -rf "${docker_dir}/logs"
mkdir -p "${docker_dir}/logs"

echo ""
echo "============================================"
echo "  Reset complete."
echo "============================================"
echo ""
