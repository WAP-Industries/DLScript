@echo off

cd /
cd /d "%~dp0"

dotnet build -c Release

if %errorlevel% equ 0 (
    echo Compilation successful
) else (
    echo Compilation failed
)