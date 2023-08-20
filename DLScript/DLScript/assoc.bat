@echo off
SETLOCAL EnableDelayedExpansion

rem set to containing folder
cd /
cd /d "%~dp0"

set "extension=.dlscript"
set "iconPath=%cd%\logo.ico"

assoc %extension% >nul 2>&1
if errorlevel 1 (
    rem associate file extension
    assoc %extension%=filetype

    rem set icon for file extension
    ftype filetype=%iconPath%

    rem refresh icon cache
    taskkill /IM explorer.exe /F >nul
    del /f /s /q "%userprofile%\AppData\Local\IconCache.db" >nul
    start explorer.exe
    echo Process ended
) else (
    echo File extension is already associated.
)

endlocal
