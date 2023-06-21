#!/bin/bash

projectFile="KOTORModSync.GUI/KOTORModSync.csproj"
publishProfilesDir="KOTORModSync.GUI/Properties/PublishProfiles"
solutionDir=$(dirname "$(readlink -f "$0")")

publishProfileFiles=$(find "$publishProfilesDir" -name "*.pubxml")

for file in $publishProfileFiles; do
    # Extract the framework name from the file name
    framework=$(basename "$file" | cut -d'_' -f1)

    # Build the dotnet publish command with the --framework argument
    command="dotnet publish $projectFile --framework $framework /p:PublishProfile=$file /p:SolutionDir=$solutionDir"

    echo "Publishing with framework: $framework"
    if eval "$command"; then
        echo "Publishing with framework '$framework' completed successfully."
    else
        errorMessage=$(eval "$command" 2>&1)
        echo "An error occurred while publishing with framework '$framework': $errorMessage"
    fi
done
