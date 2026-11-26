@echo off
setlocal

set "GAME_ROOT=%~1"
set "NO_PAUSE=0"
set "BUILD_EXIT_CODE=0"

if /I "%~1"=="/nopause" (
    set "GAME_ROOT="
    set "NO_PAUSE=1"
)
if /I "%~2"=="/nopause" set "NO_PAUSE=1"

if not defined GAME_ROOT if defined RIMWORLD_DIR set "GAME_ROOT=%RIMWORLD_DIR%"
if not defined GAME_ROOT if exist "Y:\SteamLibrary\steamapps\common\RimWorld\Assembly-CSharp.dll" set "GAME_ROOT=Y:\SteamLibrary\steamapps\common\RimWorld"
if not defined GAME_ROOT if exist "%ProgramFiles(x86)%\Steam\steamapps\common\RimWorld\Assembly-CSharp.dll" set "GAME_ROOT=%ProgramFiles(x86)%\Steam\steamapps\common\RimWorld"
if not defined GAME_ROOT if exist "%ProgramFiles%\Steam\steamapps\common\RimWorld\Assembly-CSharp.dll" set "GAME_ROOT=%ProgramFiles%\Steam\steamapps\common\RimWorld"

if not defined GAME_ROOT (
    echo ERROR: RimWorld installation was not found.
    echo Set RIMWORLD_DIR or pass the RimWorld folder as the first argument.
    set "BUILD_EXIT_CODE=1"
    goto :finish
)

echo.
echo ========================================
echo Electromagnetic Pulse Scanner - Build All
echo RimWorld: "%GAME_ROOT%"
echo ========================================
echo.

echo [1/2] Building RimWorld 1.6...
call "%~dp0Source\1.6\Build.bat" "%GAME_ROOT%" /nopause
if errorlevel 1 set "BUILD_EXIT_CODE=1"

echo.
echo [2/2] Building RimWorld 1.7...
call "%~dp0Source\1.7\Build.bat" "%GAME_ROOT%" /nopause
if errorlevel 1 set "BUILD_EXIT_CODE=1"

echo.
if "%BUILD_EXIT_CODE%"=="0" (
    echo All builds completed successfully.
    echo Output folders: 1.6\Assemblies and 1.7\Assemblies
) else (
    echo One or more builds failed. Review the messages above.
)

:finish
if "%NO_PAUSE%"=="0" pause
exit /b %BUILD_EXIT_CODE%
