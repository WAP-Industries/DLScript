@echo off
SETLOCAL ENABLEDELAYEDEXPANSION

set "extension=.dlscript"

rem check for file extension association
assoc %extension% >nul 2>&1
if errorlevel 1 (
    rem extension isnt associated
    echo false
) else (
    echo true
)

ENDLOCAL