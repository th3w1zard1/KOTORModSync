Set-Location -Path $PSScriptRoot

$version = "v1.3"
$sevenZipPath = "C:\Program Files\7-Zip\7z.exe"  # Path to 7zip executable
$gitBashLocation = "C:\Program Files\Git\bin\bash.exe"
$dotnetPath = "/mnt/c/Program Files/dotnet/dotnet.exe"
$sourceFolderStrPath = "vendor/bin"
$sourceFolder = Get-ChildItem -Path $sourceFolderStrPath

trap {
    Write-Host -ForegroundColor Red "$($_.InvocationInfo.PositionMessage)`n$($_.Exception.Message)"
    Write-Host "Press any key to continue regardless..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    continue
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

function Initialize-MacOSAppBundle {
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

function Set-UnixFilePermissions {
    param(
        [string]$path,
        [string]$permissions
    )
    $unixArchiveSource = Convert-WindowsPathToUnix -path $path
    if ($null -ne (Get-Command "wsl" -ErrorAction SilentlyContinue)) {
        $command = "& wsl chmod $permissions -Rc $unixArchiveSource"
        Write-Host $command
        Invoke-Expression $command
    } elseif ($null -ne (Get-Command "chmod" -ErrorAction SilentlyContinue)) {
        $command = "& chmod $permissions -Rc '$path'"
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
    if ($null -ne (Get-Command "wsl" -ErrorAction SilentlyContinue)) {
        $parentDir = [System.IO.Path]::GetDirectoryName($archiveSource)
        $originalDir = Get-Location
        $archiveFile = [System.IO.Path]::GetFullPath((Join-Path $originalDir $archiveFile))
        $unixArchivePath = Convert-WindowsPathToUnix -path $archiveFile
        $command = "cd '$parentDir'; & wsl zip -q -r -9 '$unixArchivePath' '$([System.IO.Path]::GetFileName($archiveSource))'; cd '$originalDir'"
        Write-Host $command
        Invoke-Expression $command
    } elseif ($null -ne (Get-Command "zip" -ErrorAction SilentlyContinue)) {
        $parentDir = [System.IO.Path]::GetDirectoryName($archiveSource)
        $originalDir = Get-Location
        $archiveFile = [System.IO.Path]::GetFullPath((Join-Path $originalDir $archiveFile))
        $command = "cd '$parentDir'; & zip -q -r -9 '$archiveFile' '$([System.IO.Path]::GetFileName($archiveSource))'; cd '$originalDir'"
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
    
    New-Item -Name "publish_holopatcher" -ItemType Directory -ErrorAction SilentlyContinue
    # Remove old builds if they exist.
    Get-ChildItem -Path "publish_holopatcher" -File | ForEach-Object {
        Remove-Item -Path $_.FullName -Force -Confirm:$false
    }
    foreach ($item in $sourceFolder) {
        Write-Host ""
        Write-Host "Zipping holopatcher: '$item'"
        Write-Host "Source path: '$($item.FullName)'"
        $fileName = [System.IO.Path]::GetFileNameWithoutExtension($item.Name)
        $fileNameParts = $fileName -split '_'

        # Fix file permissions before archiving
        Set-UnixFilePermissions -path $item.FullName -permissions "777"

        # Determine compression method
        if ($null -ne (Get-Command "wsl" -ErrorAction SilentlyContinue)) {
            $archiveFile = Join-Path -Path "publish_holopatcher" -ChildPath "$item.zip"
            Compress-Zip -archiveFile $archiveFile -archiveSource $item.FullName
        } else {
            Write-Warning "Creating .tar.gz instead of .zip archives to preserve file attributes, please run on unix or wsl if you want zips."
            $archiveFile = Join-Path -Path "publish_holopatcher" -ChildPath "$item.tar.gz"
            Compress-TarGz -archiveFile $archiveFile -archiveSource $item.FullName
        }

        Write-Host "Publishing '$item' completed successfully."
    }

    Write-Host "Zipped all HoloPatcher's successfully."
    Write-Host "Press any key to continue..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
} catch {
    Write-Host -ForegroundColor Red "$($_.InvocationInfo.PositionMessage)`n$($_.Exception.Message)"
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    continue
}
