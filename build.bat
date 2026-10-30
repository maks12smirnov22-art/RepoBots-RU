@echo off
cd /d "%~dp0"
echo Working folder: %CD%
echo.
where dotnet
if errorlevel 1 (
  echo.
  echo NET SDK NOT FOUND. Install .NET SDK 8, then run this file again.
  pause
  exit /b 1
)
echo.
echo Building, please wait a few minutes...
dotnet --version > build_log.txt 2>&1
dotnet build RepoBots.csproj -c Release >> build_log.txt 2>&1
echo.
echo DONE. Send the file build_log.txt (it is in this same folder) to Claude.
echo If the build worked, the mod is here: bin\Release\netstandard2.1\RepoBots.dll
echo.
pause
