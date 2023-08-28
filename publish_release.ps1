$version = "v0.10.1"
$projectFile = "KOTORModSync.GUI\KOTORModSync.csproj"
$publishProfilesDir = "KOTORModSync.GUI\Properties\PublishProfiles"
$sevenZipPath = "C:\Program Files\7-Zip\7z.exe"  # Path to 7zip executable
$publishProfileFiles = Get-ChildItem -Path $publishProfilesDir -Filter "*.pubxml"

# Remove old builds if they exist.
Remove-Item "bin" -Recurse -ErrorAction SilentlyContinue

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
    $publishCommand = "dotnet publish $projectFile --framework $framework /p:PublishProfile=$file"
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

        # Copy the license
        Copy-Item -Path "LICENSE.TXT" -Destination $publishFolder
        Copy-Item -Path "usage guide.txt" -Destination $publishFolder

        # Define the archive file path
        $archiveFile = Join-Path -Path (Split-Path -Path "bin\publish") -ChildPath "$rid.zip"

        # Create the archive using 7zip CLI
        $archiveCommand = "& `"$sevenZipPath`" a -tzip `"$archiveFile`" `"$publishFolder*`""
        Invoke-Expression $archiveCommand

        # Remove the leftover folder
        Remove-Item -Path "$publishFolder\..\$topLevelFolder" -Recurse

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
