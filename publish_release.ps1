$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

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
$gitBashLocation = "C:\Program Files\Git\bin\bash.exe"

# Remove old builds if they exist.
Get-ChildItem -Path "bin" -Filter "*.zip" | ForEach-Object {
    Remove-Item -Path $_.FullName -Force
}
Get-ChildItem -Path "bin" -Filter "*.tar" | ForEach-Object {
    Remove-Item -Path $_.FullName -Force
}
Get-ChildItem -Path "bin" -Filter "*.gz" | ForEach-Object {
    Remove-Item -Path $_.FullName -Force
}
Get-ChildItem -Path "bin/publish" -Recurse | ForEach-Object {
    Remove-Item -Path $_.FullName -Force -Recurse
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

function Convert-WindowsPathToUnix {
    param(
        [string]$path
    )
    $unixPath = $path -replace '\\', '/' -replace ' ', '\ '
    if ($null -ne (Get-Command "wsl" -ErrorAction SilentlyContinue)) {
        $unixPath = $unixPath -replace 'C:', '/mnt/c'
    } else {
        $unixPath = $unixPath -replace 'C:', '/c'
    }
    return $unixPath
}

function Prepare-MacOSAppBundle {
    param(
        [string]$path
    )
    $appFolder = Join-Path -Path $path -ChildPath "KOTORModSync.app"
    $contentsFolder = Join-Path -Path $appFolder -ChildPath "Contents"
    $macOSFolder = Join-Path -Path $contentsFolder -ChildPath "MacOS"

    New-Item -Path $macOSFolder -ItemType Directory -Force > $null

    Get-ChildItem -Path $path | Where-Object { $_.FullName -ne $appFolder } | ForEach-Object {
        Move-Item -Path $_.FullName -Destination $macOSFolder
    }

    $oldResourcesFolder = Join-Path -Path $macOSFolder -ChildPath "Resources"
    Move-Item -Path $oldResourcesFolder -Destination $appFolder

    Copy-Item -Path "./Info.plist" -Destination $contentsFolder

    return $appFolder
}

function Change-UnixFilePermissions {
    param(
        [string]$path,
        [string]$permissions
    )
    $unixArchiveSource = Convert-WindowsPathToUnix -path $path
    if ($null -ne (Get-Command "wsl" -ErrorAction SilentlyContinue)) {
        $command = "& wsl chmod $permissions -Rv $unixArchiveSource"
        Write-Host $command
        Invoke-Expression $command
    } else {
        $command = "& `"$gitBashLocation`" -c 'find $unixArchiveSource -type d -exec chmod $permissions {} \;'"
        Write-Host $command
        Invoke-Expression $command
        $command = "& `"$gitBashLocation`" -c 'find $unixArchiveSource -type f -exec chmod $permissions {} \;'"
        Write-Host $command
        Invoke-Expression $command
    }
}

function Compress-TarGz {
    param(
        [string]$archiveFile,
        [string]$archiveSource
    )
    $unixArchivePath = Convert-WindowsPathToUnix -path $archiveFile
    $unixArchiveSource = Convert-WindowsPathToUnix -path $archiveSource
    $folderName = [System.IO.Path]::GetFileName($archiveSource)
    $parentDir = [System.IO.Path]::GetDirectoryName($archiveSource)
    $unixParentDir = Convert-WindowsPathToUnix -path $parentDir
    if ($null -ne (Get-Command "wsl" -ErrorAction SilentlyContinue)) {
        $command = "& wsl tar -czvf $unixArchivePath -C $unixParentDir '$folderName'"
        Write-Host $command
        Invoke-Expression $command
    } elseif ($null -ne (Get-Command "tar" -ErrorAction SilentlyContinue)) {
        $command = "& tar -czvf $archivePath -C $parentDir '$folderName'"
        Write-Host $command
        Invoke-Expression $command
    } elseif ($null -ne (Get-Command "7z" -ErrorAction SilentlyContinue)) {
        $tarArchive = $archiveFile -replace '\.gz$', ''
        & '7z' a -ttar -mx=9 $tarArchive $archiveSource
        & '7z' a -tgzip -mx=9 $archiveFile $tarArchive
        Remove-Item -Path $tarArchive
    } elseif (Test-Path -Path $sevenZipPath) {
        $tarArchive = $archiveFile -replace '\.gz$', ''
        & $sevenZipPath a -ttar -mx=9 $tarArchive $archiveSource
        & $sevenZipPath a -tgzip -mx=9 $archiveFile $tarArchive
        Remove-Item -Path $tarArchive
    } elseif (Test-Path -Path $gitBashLocation) {
        $command = "& `"$gitBashLocation`" -c 'tar czf $unixArchivePath $unixArchiveSource'"
        Write-Host $command
        Invoke-Expression $command
    } else {
        Write-Error "No available method for archive creation found."
        return
    }
}

function Compress-Zip {
    param(
        [string]$archiveFile,
        [string]$archiveSource
    )
    if ($null -ne (Get-Command "zip" -ErrorAction SilentlyContinue)) {
        $parentDir = [System.IO.Path]::GetDirectoryName($archiveSource)
        $originalDir = Get-Location
        $archiveFile = [System.IO.Path]::GetFullPath((Join-Path $originalDir $archiveFile))
        $command = "cd '$parentDir'; & 'zip' -r -9 '$archiveFile' '$([System.IO.Path]::GetFileName($archiveSource))'; cd '$originalDir'"
        Write-Host $command
        Invoke-Expression $command
    } elseif ($null -ne (Get-Command "7z" -ErrorAction SilentlyContinue)) {
        $command = "& '7z' a -tzip -mx=9 `"$archiveFile`" `"$archiveSource`""
        Write-Host $command
        Invoke-Expression $command
    } elseif (Test-Path -Path $sevenZipPath) {
        $command = "& `"$sevenZipPath`" a -tzip -mx=9 `"$archiveFile`" `"$archiveSource`""
        Write-Host $command
        Invoke-Expression $command
    } else {
        $parentDir = Split-Path -Parent $archiveSource
        $newParentFolder = Join-Path $parentDir ("TempParent_" + (Get-Date).Ticks)
        New-Item -Path $newParentFolder -ItemType Directory -Force
        Move-Item -Path $archiveSource -Destination $newParentFolder
        Compress-Archive -Path "$newParentFolder\*" -DestinationPath $archiveFile -Force
        Remove-Item -Path $newParentFolder -Recurse -Force
    }
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

        # Determine if macOS
        $isMacOSTarget = $rid -eq "osx-x64" -or $rid -eq "osx-arm64"

        if ($isMacOSTarget) {
            $archiveSource = Prepare-MacOSAppBundle -path $publishFolder
        } else {
            $archiveSource = $publishFolder
        }

        # Copy the license and documentation
        Copy-Item -Path "LICENSE.TXT" -Destination $publishFolder
        Copy-Item -Path "KOTORModSync - Official Documentation.txt" -Destination $publishFolder

        # Before archiving, set *.pdb files to hidden
        Get-ChildItem -Path $archiveSource -Recurse -Filter "*.pdb" | ForEach-Object {
            Set-HiddenAttribute -path $_.FullName
        }

        $archiveFile = Join-Path -Path (Split-Path -Path "bin\publish") -ChildPath ("$rid" + $(if ($rid -like "win*") { ".zip" } else { ".tar.gz" }))

        # Fix file permissions before archiving
        Change-UnixFilePermissions -path $archiveSource -permissions "777"

        # Determine compression method
        if ($rid -like "win*") {
            Compress-Zip -archiveFile $archiveFile -archiveSource $archiveSource
        } else {
            Compress-TarGz -archiveFile $archiveFile -archiveSource $archiveSource
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

Write-Host "Built all targets successfully."
Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
