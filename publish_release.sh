#!/bin/bash

version="v0.10.43"
projectFile="KOTORModSync.GUI/KOTORModSync.csproj"
publishProfilesDir="KOTORModSync.GUI/Properties/PublishProfiles"
sevenZipPath="7z"

# Remove old builds if they exist.
rm -f bin/*.zip
rm -rf bin/publish/*

# Use chmod to change the file attribute instead of PowerShell's hidden attribute method
setHiddenAttribute() {
    chmod 600 "$1"
}

removeHiddenAttribute() {
    chmod 644 "$1"
}

# Loop over the publish profile files
for file in "$publishProfilesDir"/*.pubxml; do
    echo "Publishing configuration for '$file'"
    fileName=$(basename "$file" .pubxml)
    IFS="_" read -ra fileNameParts <<< "$fileName"

    framework="${fileNameParts[0]}"
    rid="${fileNameParts[1]}"
    cpu=$(echo "$rid" | cut -d '-' -f2)
    lastSection="${fileNameParts[2]}"

    echo "Framework: '$framework'"
    echo "RID: '$rid'"
    echo "CPU: '$cpu'"
    echo "Subfolder: '$lastSection'"

    # Build the dotnet publish command with the --framework argument
    publishCommand="dotnet publish $projectFile -c Release --framework $framework -p:PublishProfile=$file"
    echo "Publish command: $publishCommand"

    # Execute the publish command
    if $publishCommand; then

        # Define the top-level folder
        topLevelFolder="KOTORModSync $version"

        # Get the publish folder path
        if [ -z "$lastSection" ]; then
            publishFolder="./bin/publish/$framework/$rid"
        else
            publishFolder="./bin/publish/$lastSection/$framework/$rid"
        fi

        # Rename for our top-level folder for the archive
        mv "$publishFolder" "./bin/publish/$lastSection/$framework/$topLevelFolder"

        # Reset the publish folder path
        if [ -z "$lastSection" ]; then
            publishFolder="./bin/publish/$framework/$topLevelFolder"
        else
            publishFolder="./bin/publish/$lastSection/$framework/$topLevelFolder"
        fi

        # Ensure 'docs' folder exists inside the publishFolder
        if [ ! -d "$publishFolder/docs" ]; then
            mkdir "$publishFolder/docs"
        fi

        # Copy the license and documentation into the 'docs' folder
        cp "LICENSE.TXT" "$publishFolder/docs"
        cp "KOTORModSync - Official Documentation.txt" "$publishFolder/docs"

        # Define the archive file path
        archiveFile="./bin/$rid.zip"

        # Before archiving, set *.pdb files to hidden
        find "$publishFolder" -name "*.pdb" -exec chmod 600 {} \;

        # Create the archive using 7zip CLI
        $sevenZipPath a -tzip "$archiveFile" "$publishFolder/*"

        # Remove the leftover folder
        rm -rf "$publishFolder"

        echo "Publishing with framework '$framework' completed successfully."

    else
        echo "An error occurred while publishing with framework '$framework'"
    fi
done

echo "Built all targets."
read -n1 -rsp $'Press any key to continue...\n'
