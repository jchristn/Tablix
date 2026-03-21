@echo off
echo Cleaning Tablix server data...

if exist tablix.json (
    del tablix.json
    echo Deleted tablix.json
)

if exist database.db (
    del database.db
    echo Deleted database.db
)

if exist logs\ (
    del /q logs\*
    echo Cleaned logs/
)

echo Done.
