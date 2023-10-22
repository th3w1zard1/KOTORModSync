$ErrorActionPreference = 'Stop'

trap {
    Write-Host -ForegroundColor Red "$($_.InvocationInfo.PositionMessage)`n$($_.Exception.Message)"
    Write-Host "Press any key to continue regardless..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    continue
}


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
Get-ChildItem -Path "bin" -Filter "*.tar.gz" | ForEach-Object {
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
try {
    foreach ($file in $publishProfileFiles) {
        Write-Host ""
        Write-Host "Publishing configuration for '$file'"
        $fileName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $fileNameParts = $fileName -split '_'

        $framework = $fileNameParts[0]
        $rid = $fileNameParts[1]
        $lastSection = $fileNameParts[2]

        # Build the dotnet publish command with the --framework argument
        $publishCommand = "dotnet publish $projectFile -c Release --framework $framework /p:PublishProfile=$file 2>&1 | Out-Null"

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

        # Determine if macOS
        $isMacOSTarget = $rid -eq "osx-x64" -or $rid -eq "osx-arm64"
        $resourcesFolder = Join-Path -Path $publishFolder -ChildPath "Resources"

        # macOS specific .app structure creation
        if ($isMacOSTarget) {
            $appFolder = Join-Path -Path $publishFolder -ChildPath "KOTORModSync.app"
            $contentsFolder = Join-Path -Path $appFolder -ChildPath "Contents"
            $macOSFolder = Join-Path -Path $contentsFolder -ChildPath "MacOS"
            $resourcesFolder = Join-Path -Path $appFolder -ChildPath "Resources"

            # Create the directories
            New-Item -Path $macOSFolder -ItemType Directory -Force > $null

            # Move all published files to the MacOS directory
            Get-ChildItem -Path $publishFolder | Where-Object { $_.FullName -ne $appFolder } | ForEach-Object {
                Move-Item -Path $_.FullName -Destination $macOSFolder
            }

            # Move the Resources folder from MacOS to the App root
            $oldResourcesFolder = Join-Path -Path $macOSFolder -ChildPath "Resources"
            Move-Item -Path $oldResourcesFolder -Destination $appFolder

            # Copy Info.plist to the Contents folder
            Copy-Item -Path "./Info.plist" -Destination $contentsFolder
        }

        # Copy the license and documentation
        Copy-Item -Path "LICENSE.TXT" -Destination $publishFolder
        Copy-Item -Path "KOTORModSync - Official Documentation.txt" -Destination $publishFolder

        if ($isMacOSTarget) {
            # Set the directory for the zip operation to the .app folder
            $archiveSource = $appFolder
        } else {
            $archiveSource = $publishFolder
        }

        # Before archiving, set *.pdb files to hidden
        Get-ChildItem -Path $archiveSource -Recurse -Filter "*.pdb" | ForEach-Object {
            Set-HiddenAttribute -path $_.FullName
        }

        # Check if WSL is available
        $wslAvailable = $null -ne (Get-Command "wsl" -ErrorAction SilentlyContinue)

        # Determine the file extension based on $rid
        if ($rid -like "win*") {
            $extension = ".zip"
        } else {
            $extension = ".tar.gz"
        }

        $archiveFile = Join-Path -Path (Split-Path -Path "bin\publish") -ChildPath ("$rid" + $extension)
        # Convert Windows path to Unix-like path for usage in Git Bash
        $gitBashArchivePath = $archiveFile -replace '\\', '/' -replace 'C:', '/c' -replace ' ', '\ '
        $gitBashArchiveSource = $archiveSource -replace '\\', '/' -replace 'C:', '/c' -replace ' ', '\ '
        # Convert Windows path to Unix-like path for usage in WSL
        $unixArchivePath = $archiveFile -replace '\\', '/' -replace 'C:', '/mnt/c' -replace ' ', '\ '
        $unixArchiveSource = $archiveSource -replace '\\', '/' -replace 'C:', '/mnt/c' -replace ' ', '\ '

        # Check extension to determine compression method
        if ($extension -eq ".zip") {
            if ($null -ne (Get-Command "zip" -ErrorAction SilentlyContinue)) {
                $parentDir = [System.IO.Path]::GetDirectoryName($archiveSource)
                $originalDir = Get-Location
                $archiveFile = [System.IO.Path]::GetFullPath((Join-Path $originalDir $archiveFile))

                Set-Location -Path $parentDir
                $command = "cd '$parentDir'; & 'zip' -r -9 '$archiveFile' '$([System.IO.Path]::GetFileName($archiveSource))'; cd '$originalDir'"
                Write-Host $command
                Invoke-Expression $command
                Set-Location -Path $originalDir
            } elseif ($null -ne (Get-Command "7z" -ErrorAction SilentlyContinue)) {
                & '7z' a -tzip $archiveFile $archiveSource
            } else {
                # Determine parent directory of $archiveSource
                $parentDir = Split-Path -Parent $archiveSource

                # Create a unique temporary parent folder name in the same location as $archiveSource
                $newParentFolder = Join-Path $parentDir ("TempParent_" + (Get-Date).Ticks)
                New-Item -Path $newParentFolder -ItemType Directory -Force

                # Move $archiveSource into the new parent folder
                Move-Item -Path $archiveSource -Destination $newParentFolder

                # Run Compress-Archive on the new parent folder
                Compress-Archive -Path "$newParentFolder\*" -DestinationPath $archiveFile -Force

                # Remove the new parent folder
                Remove-Item -Path $newParentFolder -Recurse -Force
            }
        } else {
            # Change permissions to 777 for all files and directories
            if ($wslAvailable) {
                $command = "& wsl chmod 777 -Rv $unixArchiveSource"
                Write-Host $command
                Invoke-Expression $command
            } else { # use git bash
                $command = "& `"$bashLocation`" -c 'find $gitBashArchiveSource -type d -exec chmod 777 {} \;'"
                Write-Host $command
                Invoke-Expression $command
                $command = "& `"$bashLocation`" -c 'find $gitBashArchiveSource -type f -exec chmod 777 {} \;'"
                Write-Host $command
                Invoke-Expression $command
            }
            if ($wslAvailable) {
                $parentDir = [System.IO.Path]::GetFileName($archiveSource)
                $unixParentDir = $parentDir -replace '\\', '/' -replace 'C:', '/mnt/c' -replace ' ', '\ '
                $originalDir = Get-Location
                $archiveFile = [System.IO.Path]::GetFullPath((Join-Path $originalDir $archiveFile))
                $unixArchivePath = $archiveFile -replace '\\', '/' -replace 'C:', '/mnt/c' -replace ' ', '\ '
                $unixArchiveSource = [System.IO.Path]::GetDirectoryName($archiveSource) -replace '\\', '/' -replace 'C:', '/mnt/c' -replace ' ', '\ '

                $command = "& wsl tar -czvf $unixArchivePath -C $unixArchiveSource $unixParentDir"
                Write-Host $command
                Invoke-Expression $command
            } else {
                # Check if 7z is available
                $sevenZipAvailable = $null -ne (Get-Command "7z" -ErrorAction SilentlyContinue)
                if ($sevenZipAvailable) {
                    # Use 7z to create a .tar.gz
                    $tarArchive = $archiveFile -replace '\.gz$', ''  # Remove the .gz extension for intermediate .tar file
                    & '7z' a -ttar -mx=9 $tarArchive $archiveSource
                    & '7z' a -tgzip -mx=9 $archiveFile $tarArchive
                    Remove-Item -Path $tarArchive  # Delete the intermediate .tar file
                } else { # Fallback to git bash method if 7z isn't available
                    # Use tar to compress
                    $command = "& `"$bashLocation`" -c 'tar czf $gitBashArchivePath $gitBashArchiveSource'"
                    Write-Host $command
                    Invoke-Expression $command
                }
            }
        }

        # Before deleting, set *.pdb files back to normal
        Get-ChildItem -Path "$publishFolder\..\$topLevelFolder" -Recurse -Filter "*.pdb" | ForEach-Object {
            Remove-HiddenAttribute -path $_.FullName
        }

        # Remove the leftover folders
        Remove-Item -Path "bin\publish" -Recurse -Force

        Write-Host "Publishing '$file' completed successfully."
    }
} catch {
    Write-Host -ForegroundColor Red "$($_.InvocationInfo.PositionMessage)`n$($_.Exception.Message)"
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    continue
}

Write-Host "Built all targets."
Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
