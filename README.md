# KOTORModSync
KOTORModSync is a multi-mod installer for KOTOR games that makes it easier to install and manage mods. I usually install the Reddit modbuild every year or so. The last time I did so I made a mistake on a single different step 3 times in a row and had to start over each time. So I decided to create an installer creator in C# to simplify the process.

![image](https://github.com/th3w1zard1/KOTORModSync/assets/2219836/beb2259b-0417-4a75-bc10-8de0345b3e2f)


## Goals
Mod creators work really hard on their mods. It's the least we can do to install them and use them, right? However who wants to reinstall to vanilla just to spend several hours reinstalling mods, just to add 1 or 2 extra mods on top of it?
Other modmanagers I've tried were either too difficult to configure, requiring significant changes to a hard-to-understand configuration file; or only provided limited functionality for defining new mods. KOTOR mods do have complex dependency relationships with each other in regards to compatibility.

Enter KOTORMODSync.

This installer creator has a built-in GUI editor, and provides serializers to parse and load the TOML instruction file. Modbuild creators can create instructions with little to no knowledge of the format and easily share with end users. End users can install everything from the instruction file with a simple click of a button. Edit any instruction and verify the configuration with built-in tools. There's also some support to dry run an install.

Once there's been enough success stories and enough testers, the program will provide file hashes to accompany the instructions file. This will find and catch most unforseen errors during installation.


## Features
Can install the r/KOTOR modbuilds in about 20 minutes from a vanilla install.
A flexible configuration editor and parser utilizing TOML syntax. This is very similar to INI which TSLPatcher already uses and most modders are used to.
Create instructions files with complex dependency structures for multiple mods, and have end users install everything exactly according to the instructions created. No more manually copying/deleting files: KOTORModSync handles all of that for your end user.
Supports file validation using hashes of a game install and mod files to ensure each mod installed correctly. In order for this to work, this assumes the user started with a vanilla install.

## Platforms
KOTORModSync is compatible with Windows7-11, Linux, and Mac, as well as anything supported by .NET Standard 2.0.

### Linux/Mac
You need NET6 or higher, the .NET Framework version I believe isn't compatible. You may need additional X11 development libraries. In order to get this working on WSL I had to install the following packages:

`sudo apt install libsm6 libice6 libx11-dev libfontconfig1 libx11-6 libx11-xcb1 libxau6 libxcb1 libxdmcp6 libxcb-xkb1 libxcb-render0 libxcb-shm0 libxcb-xfixes0 libxcb-util1 libxcb-xinerama0 libxcb-randr0 libxcb-image0 libxcb-keysyms1 libxcb-sync1 libxcb-xtest0`

Then you can simply run the EXE like this in a terminal:

`./KOTORModSync.exe`

## Troubleshooting
​
When launching the application, if you see some text in the console and an instant crash to desktop, the most probable cause is you're using the wrong NET version.

There's a log file created in the same location as the EXE that'll have the details. That log file is named:
#### `kotormodsync_<date>.txt`

It's important that you download the .NET version that closely matches the one your os uses. This will USUALLY be the following:

Windows 10, 11, Linux, and Mac: Download `NET_6_0.zip`

Windows Vista, 7, and 8: Determine whether you're on an x86 or x64 platform and download `NET_FRAMEWORK_46x_(PLATFORM).zip`

## Build instructions
I'm honestly not sure what all you need, I was able to build and run it on both vs2019 and vs2022. From what I understand, the minimum build requirements are:
- NET 6 or NET462 targeting platform and build tools
- .NET Development Kit.

All you need to do is build KOTORModSync.GUI. This should build the program into ./KOTORModSync.GUI/bin directory. Or run the command `dotnet build` then `dotnet run` inside KOTORModSync.GUI folder.

#### KOTORModSync.GUI
- Main GUI running on AvaloniaUI v0.10.x
#### KOTORModSync.Core
- All the main logic is defined here.
- Targets .net standard 2.0
#### KOTORModSync.ConsoleApp
- Contains a few quickly written developer tools used to quickly test some features.


## Credit
#### Snigaroo
This man saved me countless amount of hours. I'd still be struggling through game glitches and mod-specific instructions I didn't understand. Actually I'd probably still be on Dantooine trying to determine why I'm getting visual glitches and crashes which he solved with the one word message 'grass'.

#### JCarter426
Ditto. There was so much KOTOR-specific things to learn, without his help I'd still be trying to deserialize encapsulated resource files. His time and patience were incredibly useful to the project and this project would be impossible without him.

### Testers:
##### Lewok from r/KOTOR
Thank you for helping test that obnoxious UAC elevation problem legacy Windows apps like TSLPatcher has!

#### Other notable users
#### @Woodske
Created TSLPatcherCLI used in the project.

#### Fair-Strides
Made TSLPatcher open-source on Github

#### Stoffe
Original creator of TSLPatcher

Thank you to the entire KOTOR community.
