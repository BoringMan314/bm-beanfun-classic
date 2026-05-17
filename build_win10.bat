@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "SLN=Beanfun.sln"
set "CSPROJ=Beanfun\Beanfun.csproj"
set "EXE_NAME=BeanfunClassic.exe"
set "OUT_DIR=%~dp0dist"

echo [build_win10] Beanfun Classic Win10/x64: self-contained single-file publish -^> dist\%EXE_NAME%

taskkill /F /IM "%EXE_NAME%" /T >nul 2>&1

if not exist "dist" mkdir "dist" 2>nul
call :clean_dir_contents "dist"

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [build_win10] FAIL: dotnet SDK not found in PATH
  goto :end_fail
)

echo [build_win10] using:
dotnet --version

dotnet restore "%SLN%"
if errorlevel 1 (
  echo [build_win10] FAIL: dotnet restore
  goto :end_fail
)

rem Match .github/workflows/build-and-release.yml publish settings
rem Use trailing / inside quotes - CMD treats \" at end of path as escaped quote and breaks -o
dotnet publish "%CSPROJ%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "%OUT_DIR%"
if errorlevel 1 (
  echo [build_win10] FAIL: dotnet publish
  goto :end_fail
)

if not exist "%OUT_DIR%\%EXE_NAME%" (
  echo [build_win10] FAIL: missing %OUT_DIR%\%EXE_NAME%
  goto :end_fail
)

echo [build_win10] OK: %OUT_DIR%\%EXE_NAME%
goto :end_ok

:clean_dir_contents
set "TGT=%~1"
if not exist "%TGT%" exit /b 0
for /f "delims=" %%D in ('dir /b /ad "%TGT%" 2^>nul') do rd /s /q "%TGT%\%%D" 2>nul
del /f /q "%TGT%\*" 2>nul
exit /b 0

:end_fail
if /i "%~1"=="nopause" exit /b 1
echo.
pause
exit /b 1

:end_ok
if /i "%~1"=="nopause" exit /b 0
echo.
pause
exit /b 0
