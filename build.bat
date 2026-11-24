@echo off
cd /d "%~dp0"
echo ============================================
echo   Building Weep for the Departed for tModLoader
echo ============================================
dotnet build -c Debug
if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCCESS] Mod built and packaged successfully!
) else (
    echo.
    echo [ERROR] Build failed with exit code %ERRORLEVEL%.
)
echo.
pause
