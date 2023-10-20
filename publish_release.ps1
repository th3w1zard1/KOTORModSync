$version = "v0.10.43"
$projectFile = "KOTORModSync.GUI\KOTORModSync.csproj"
$publishProfilesDir = "KOTORModSync.GUI\Properties\PublishProfiles"
$sevenZipPath = "C:\Program Files\7-Zip\7z.exe"  # Path to 7zip executable
$publishProfileFiles = Get-ChildItem -Path $publishProfilesDir -Filter "*.pubxml"
$bashLocation = "C:\Program Files\Git\bin\bash.exe"

# Remove old builds if they exist.
Get-ChildItem -Path "bin" -Filter "*.zip" | ForEach-Object {
    Remove-Item -Path $_.FullName -Force -Confirm:$false
}
Get-ChildItem -Path "bin/publish" -Recurse | ForEach-Object {
    Remove-Item -Path $_.FullName -Force -Confirm:$false -Recurse
}

function Set-HiddenAttribute {
    param (
        [string]$path
    )
    $file = Get-Item $path
    $file.Attributes = 'Hidden'
}

function Remove-HiddenAttribute {
    param (
        [string]$path
    )
    $file = Get-Item $path
    $file.Attributes = 'Normal'
}

foreach ($file in $publishProfileFiles) {
    Write-Host "Publishing configuration for '$file'"
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $fileNameParts = $fileName -split '_'

    $framework = $fileNameParts[0]
    $rid = $fileNameParts[1]
    $cpu = ($rid -split '-')[1]
    $lastSection = $fileNameParts[2]

    Write-Host "Framework: '$framework'"
    Write-Host "RID: '$rid'"
    Write-Host "CPU: '$cpu'"
    Write-Host "Subfolder: '$lastSection'"


    # Build the dotnet publish command with the --framework argument
    $publishCommand = "dotnet publish $projectFile -c Release --framework $framework /p:PublishProfile=$file"
    Write-Host "Publish command: $publishCommand"

    try {

        # Execute the publish command
        Invoke-Expression $publishCommand

        $topLevelFolder = "KOTORModSync $version"

        # Get the publish folder path
        $publishFolder = Get-Item (Join-Path -Path (Split-Path -Path $projectFile) -ChildPath "..\bin\publish\$lastSection\$framework\$rid")

        # Rename for our top level folder for the archive.
        Rename-Item -Path $publishFolder -NewName $topLevelFolder

        $publishFolder = Get-Item (Join-Path -Path (Split-Path -Path $projectFile) -ChildPath "..\bin\publish\$lastSection\$framework\$topLevelFolder")

        # Set executable flag on our main EXE
        $potentialExePaths = @(
            (Join-Path -Path $publishFolder -ChildPath "KOTORModSync"),
            (Join-Path -Path $publishFolder -ChildPath "KOTORModSync.exe")
        )

        # For EXE paths
        foreach ($exePath in $potentialExePaths) {
            if (Test-Path $exePath) {
                Write-Host ""
                Write-Host "Fixing EXE: $exePath"
                $unixPath = $exePath -replace '\\', '/' -replace 'C:', '/c' -replace ' ', '\ '
                $command = "& `"$bashLocation`" -c 'chmod +x $unixPath -v'"
                Write-Host $command
                Invoke-Expression $command
            }
        }

        # Determine if macOS
        $isMacOS = $rid -eq "osx-x64" -or $rid -eq "osx-arm64"
        $destinationResourcesFolder = Join-Path -Path $publishFolder -ChildPath "Resources"

        # macOS specific .app structure creation
        if ($isMacOS) {
            $appFolder = Join-Path -Path $publishFolder -ChildPath "KOTORModSync.app"
            $contentsFolder = Join-Path -Path $appFolder -ChildPath "Contents"
            $macOSFolder = Join-Path -Path $contentsFolder -ChildPath "MacOS"
            $resourcesFolder = Join-Path -Path $contentsFolder -ChildPath "Resources"

            # Create the directories
            New-Item -Path $macOSFolder -ItemType Directory -Force

            # Move all published files to the MacOS directory
            Get-ChildItem -Path $publishFolder | Where-Object { $_.FullName -ne $appFolder } | ForEach-Object {
                Move-Item -Path $_.FullName -Destination $macOSFolder
            }

            # Move the Resources folder from MacOS to the App root
            $sourceResourcesFolder = Join-Path -Path $macOSFolder -ChildPath "Resources"
            $destinationResourcesFolder = Join-Path -Path $appFolder -ChildPath "Resources"
            Move-Item -Path $sourceResourcesFolder -Destination $destinationResourcesFolder

            # Copy Info.plist to the Contents folder
            Copy-Item -Path "./Info.plist" -Destination $contentsFolder
        }

        # Set executable permissions on our resources (holopatcher etc).
        $resourceFiles = Get-ChildItem -Path $destinationResourcesFolder -Recurse -File
        foreach ($file in $resourceFiles) {
            if (Test-Path $file.FullName) {
                Write-Host ""
                $filePath = $file.FullName
                Write-Host "Fixing resource: $filePath"
                $unixPath = $filePath -replace '\\', '/' -replace 'C:', '/c' -replace ' ', '\ '
                $command = "& `"$bashLocation`" -c 'chmod +x $unixPath -v'"
                Write-Host $command
                Invoke-Expression $command
            }
        }

        # Ensure 'docs' folder exists inside the $publishFolder
        $docsFolder = Join-Path -Path $publishFolder -ChildPath "docs"
        if (-not (Test-Path $docsFolder)) {
            New-Item -Path $docsFolder -ItemType Directory
        }

        # Copy the license and documentation into the 'docs' folder
        Copy-Item -Path "LICENSE.TXT" -Destination $docsFolder
        Copy-Item -Path "KOTORModSync - Official Documentation.txt" -Destination $docsFolder

        if ($isMacOS) {
            # Set the directory for the zip operation to the .app folder
            $archiveSource = $appFolder
        } else {
            $archiveSource = $publishFolder
        }

        # Before archiving, set *.pdb files to hidden
        Get-ChildItem -Path $archiveSource -Recurse -Filter "*.pdb" | ForEach-Object {
            Set-HiddenAttribute -path $_.FullName
        }

        # Define the archive file path
        $archiveFile = Join-Path -Path (Split-Path -Path "bin\publish") -ChildPath "$rid.zip"

        # Create the archive using 7zip CLI
        $archiveCommand = "& `"$sevenZipPath`" a -tzip `"$archiveFile`" `"$archiveSource*`""
        Invoke-Expression $archiveCommand

        # Before deleting, set *.pdb files back to normal
        Get-ChildItem -Path "$publishFolder\..\$topLevelFolder" -Recurse -Filter "*.pdb" | ForEach-Object {
            Remove-HiddenAttribute -path $_.FullName
        }

        # Remove the leftover folder
        Remove-Item -Path "$publishFolder\..\$topLevelFolder" -Recurse -Force

        Write-Host "Publishing with framework '$framework' completed successfully."
    }
    catch {
        $errorMessage = $_.Exception.Message
        Write-Host "An error occurred while publishing with framework '$framework': $errorMessage"
    }
}

Write-Host "Built all targets."
Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
