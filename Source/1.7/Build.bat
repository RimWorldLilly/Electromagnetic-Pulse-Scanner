@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PROJECT=%SCRIPT_DIR%ElectromagneticPulseGenerator.csproj"
set "GAME_ROOT=%~1"
set "NO_PAUSE=0"
set "BUILD_EXIT_CODE=0"

if /I "%~1"=="/nopause" (
    set "GAME_ROOT="
    set "NO_PAUSE=1"
)
if /I "%~2"=="/nopause" set "NO_PAUSE=1"

if not defined GAME_ROOT if defined RIMWORLD_DIR set "GAME_ROOT=%RIMWORLD_DIR%"
if not defined GAME_ROOT if exist "Y:\SteamLibrary\steamapps\common\RimWorld\UnityStubs.dll" set "GAME_ROOT=Y:\SteamLibrary\steamapps\common\RimWorld"
if not defined GAME_ROOT if exist "%ProgramFiles(x86)%\Steam\steamapps\common\RimWorld\UnityStubs.dll" set "GAME_ROOT=%ProgramFiles(x86)%\Steam\steamapps\common\RimWorld"
if not defined GAME_ROOT if exist "%ProgramFiles%\Steam\steamapps\common\RimWorld\UnityStubs.dll" set "GAME_ROOT=%ProgramFiles%\Steam\steamapps\common\RimWorld"

if not defined GAME_ROOT (
    echo ERROR: RimWorld installation was not found.
    echo Set RIMWORLD_DIR or pass the RimWorld folder as the first argument.
    set "BUILD_EXIT_CODE=1"
    goto :finish
)

if not exist "%PROJECT%" (
    echo ERROR: Project file not found: "%PROJECT%"
    set "BUILD_EXIT_CODE=1"
    goto :finish
)
if not exist "%GAME_ROOT%\Assembly-CSharp.dll" (
    echo ERROR: RimWorld 1.7 Assembly-CSharp.dll not found: "%GAME_ROOT%\Assembly-CSharp.dll"
    set "BUILD_EXIT_CODE=1"
    goto :finish
)
if not exist "%GAME_ROOT%\UnityStubs.dll" (
    echo ERROR: RimWorld 1.7 UnityStubs.dll not found: "%GAME_ROOT%\UnityStubs.dll"
    set "BUILD_EXIT_CODE=1"
    goto :finish
)
where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: Install the .NET SDK before building this mod.
    set "BUILD_EXIT_CODE=1"
    goto :finish
)

echo Building Electromagnetic Pulse Scanner for RimWorld 1.7...
dotnet build "%PROJECT%" --configuration Release --nologo -p:RimWorldDir="%GAME_ROOT%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"

if "%BUILD_EXIT_CODE%"=="0" (
    echo.
    echo Build succeeded:
    echo "%SCRIPT_DIR%..\..\1.7\Assemblies\ElectromagneticPulseGenerator.dll"
)

:finish
if not "%BUILD_EXIT_CODE%"=="0" echo Build failed with exit code %BUILD_EXIT_CODE%.
if "%NO_PAUSE%"=="0" pause
exit /b %BUILD_EXIT_CODE%
