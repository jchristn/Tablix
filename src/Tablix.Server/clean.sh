#!/bin/bash
echo "Cleaning Tablix server data..."

if [ -f tablix.json ]; then
    rm tablix.json
    echo "Deleted tablix.json"
fi

if [ -f database.db ]; then
    rm database.db
    echo "Deleted database.db"
fi

echo "Done."
