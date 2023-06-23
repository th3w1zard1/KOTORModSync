$projectFile = "KOTORModSync.GUI\KOTORModSync.csproj"
$publishProfilesDir = "KOTORModSync.GUI\Properties\PublishProfiles"

$publishProfileFiles = Get-ChildItem -Path $publishProfilesDir -Filter "*.pubxml"

foreach ($file in $publishProfileFiles) {
    # Extract the framework name from the file name
    $framework = $file.Name.Substring(0, $file.Name.IndexOf('_'))

    # Build the dotnet publish command with the --framework argument
    $command = "dotnet publish $projectFile --framework $framework /p:PublishProfile=$file"

    Write-Host "Publishing with framework: $framework"
    try {
        # Execute the command
        Invoke-Expression $command

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
