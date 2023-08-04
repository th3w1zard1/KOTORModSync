@echo off
setlocal EnableDelayedExpansion

set kmsversion=v0.9.0
set projectFile=KOTORModSync.GUI\KOTORModSync.csproj
set publishProfilesDir=KOTORModSync.GUI\Properties\PublishProfiles
set sevenZipPath=C:\Program Files\7-Zip\7z.exe

:: Remove old builds if they exist
if exist bin (
    rmdir /s /q bin
)

:: Loop through all pubxml files in the specified directory
for /r "%publishProfilesDir%" %%G in (*.pubxml) do (
    echo Publishing configuration for '%%G'

    :: Get file name without extension
    for %%H in (%%~nG) do set fileName=%%H
    
    :: Split the file name into parts
    for /f "tokens=1,2,3 delims=_" %%I in ("!fileName!") do (
        set framework=%%I
        set rid=%%J
        set lastSection=%%K
    )

    :: Split the RID on '-'
    for /f "tokens=2 delims=-" %%L in ("!rid!") do (
        set cpu=%%L
    )

    echo Framework: '!framework!'
    echo RID: '!rid!'
    echo CPU: '!cpu!'
    echo Subfolder: '!lastSection!'

    :: Build the dotnet publish command with the --framework argument
    set publishCommand=dotnet publish %projectFile% --framework !framework! /p:PublishProfile=!fileName!.pubxml
    echo Publish command: !publishCommand!

    :: Execute the publish command
    call !publishCommand!

    set topLevelFolder=KOTORModSync !kmsversion!
    set publishFolder=..\bin\publish\!lastSection!\!framework!\!rid!

    :: Rename for our top level folder for the archive
    ren !publishFolder! !topLevelFolder!
    set publishFolder=..\bin\publish\!lastSection!\!framework!\!topLevelFolder!

    :: Copy the license and usage guide
    copy "LICENSE.TXT" !publishFolder!
    copy "usage guide.txt" !publishFolder!

    :: Define the archive file path
    set archiveFile=bin\publish\!rid!.zip

    :: Create the archive using 7zip CLI
    "!sevenZipPath!" a -tzip "!archiveFile!" "!publishFolder*"

    :: Remove the leftover folder
    rmdir /s /q "!publishFolder!\..\!topLevelFolder!"

    echo Publishing with framework '!framework!' completed successfully.
)

echo Built all targets.
echo Press any key to continue...
pause >nul
