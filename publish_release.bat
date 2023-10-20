@echo off
SETLOCAL ENABLEDELAYEDEXPANSION

set kms_version=v0.10.43
set projectFile=KOTORModSync.GUI\KOTORModSync.csproj
set publishProfilesDir=KOTORModSync.GUI\Properties\PublishProfiles
set sevenZipPath="C:\Program Files\7-Zip\7z.exe"

:: Remove old builds if they exist.
del /F /Q bin\*.zip
for /d %%x in (bin\publish\*) do rmdir /S /Q "%%x"

for /r "%publishProfilesDir%" %%i in (*.pubxml) do (
    set fileName=%%~ni
    echo Publishing configuration for '!fileName!'

    for /f "tokens=1-3 delims=_" %%a in ("!fileName!") do (
        set framework=%%a
        set rid=%%b
        set lastSection=%%c
    )

    :: Extract the CPU from the RID
    for /f "tokens=2 delims=-" %%a in ("!rid!") do set cpu=%%a

    echo Framework: '!framework!'
    echo RID: '!rid!'
    echo CPU: '!cpu!'
    echo Subfolder: '!lastSection!'

    :: Build the dotnet publish command with the --framework argument
    set publishCommand=dotnet publish %projectFile% -c Release --framework !framework! /p:PublishProfile=!fileName!.pubxml
    echo Publish command: !publishCommand!

    :: Execute the publish command
    call !publishCommand!
    
    set topLevelFolder=KOTORModSync !kms_version!

    :: Get the publish folder path
    IF "!lastSection!"=="" (
        set publishFolder=.\bin\publish\!framework!\!rid!
    ) ELSE (
        set publishFolder=.\bin\publish\!lastSection!\!framework!\!rid!
    )

    :: Rename for our top level folder for the archive.
    rename "!publishFolder!" "!topLevelFolder!"
    IF "!lastSection!"=="" (
        set publishFolder=.\bin\publish\!framework!\!topLevelFolder!
    ) ELSE (
        set publishFolder=.\bin\publish\!lastSection!\!framework!\!topLevelFolder!
    )

    :: Ensure 'docs' folder exists inside the publishFolder
    if not exist "!publishFolder!\docs" mkdir "!publishFolder!\docs"

    :: Copy the license and documentation into the 'docs' folder
    copy /Y "LICENSE.TXT" "!publishFolder!\docs"
    copy /Y "KOTORModSync - Official Documentation.txt" "!publishFolder!\docs"
    
    :: Define the archive file path
    set archiveFile=bin\!rid!.zip

    :: Create the archive using 7zip CLI
    %sevenZipPath% a -tzip "!archiveFile!" "!publishFolder!*"

    :: Remove the leftover folder
    rmdir /S /Q "!publishFolder!"

    echo Publishing with framework '!framework!' completed successfully.
)

echo Built all targets.
pause

ENDLOCAL
