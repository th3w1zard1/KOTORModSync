@echo off

set projectFile=KOTORModSync.GUI\KOTORModSync.csproj
set publishProfilesDir=KOTORModSync.GUI\Properties\PublishProfiles
set solutionDir=%~dp0

setlocal enabledelayedexpansion

for /r "%publishProfilesDir%" %%F in (*.pubxml) do (
    set "file=%%F"

    REM Extract the framework name from the file name
    set "filename=%%~nxF"
    for /f "delims=_" %%A in ("!filename!") do set "framework=%%A"

    REM Build the dotnet publish command with the --framework argument
    set "command=dotnet publish %projectFile% --framework !framework! /p:PublishProfile=\"%%file%%\" /p:SolutionDir=%solutionDir%"

    REM Execute the command
    echo Publishing with framework: !framework!
    call !command!

    if !errorlevel! equ 0 (
        echo Publishing with framework '!framework!' completed successfully.
    ) else (
        echo An error occurred while publishing with framework '!framework!': !errorlevel!
    )
)
echo Built all targets.
pause