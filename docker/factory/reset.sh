#!/bin/bash

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
docker compose -f ../compose.yaml down

echo ""
echo "Restoring factory database..."
cp -f factory/database.db ../database.db

echo ""
echo "Restoring factory configuration..."
cp -f factory/tablix.json ../tablix.json

echo ""
echo "Clearing logs..."
rm -rf ../logs
mkdir -p ../logs

echo ""
echo "============================================"
echo "  Reset complete."
echo "============================================"
echo ""
