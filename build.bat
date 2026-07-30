@echo off
echo ========================================================
echo Building Lightweight C# WPF Smart Lamp Studio
echo (Framework-Dependent Single File Executable win-x64)
echo ========================================================

if not exist "dist" mkdir dist

dotnet publish src\SmartLampApp.csproj -c Release -r win-x64 --self-contained false -o dist /p:PublishSingleFile=true

if %ERRORLEVEL% EQU 0 (
    copy /y src\Assets\app_icon.ico dist\app_icon.ico >nul
    echo.
    echo ========================================================
    echo BUILD SUCCESSFUL!
    echo Framework-Dependent Executable created at: dist\SmartLampApp.exe
    echo ========================================================
) else (
    echo.
    echo BUILD FAILED with exit code %ERRORLEVEL%
)
