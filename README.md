# KOTORModSync
KOTORModSync is a multi-mod installer for KOTOR games that makes it easier to install and manage mods.

I usually install the Reddit mod build every year or so. The process takes about an hour and it's repetitive moving files, running TSLPatcher, deleting specific files, and occasionally renaming some files. The last time I installed the modbuild I made a mistake on a single different step, 3 times in a row. Most mistakes require a full restart from the beginning.
This is tedious, so I decided to create an installer creator in C# to simplify the process.

## Goals
Mod creators work really hard on their mods. It's the least we can do to install them and use them, right? However, who wants to reinstall to vanilla and spend several hours reinstalling mods, just to add 1 or 2 extra mods on top of it?
Other mod managers I've tried were either too difficult to configure, require significant changes to a hard-to-understand configuration file, or only provided limited functionality for defining new mods. KOTOR mods definitely can have complex dependency relationships with each other in regard to compatibility, due to the nature of TSLPatcher and KOTOR itself.

### Enter KOTORMODSync.
![image](https://github.com/th3w1zard1/KOTORModSync/assets/2219836/1d3afdbb-24cf-428b-93e1-37b61f48aa20)



## Features
- Can install the https://kotor.neocities.org/ mod builds in about 20 minutes from a vanilla install.
- Supports TSLPatching on Mac/Linux without wine!
- Select the individual mods you want for an install - the dependencies and incompatibilities will automatically be chosen and sorted. This means end users don't have to worry about specific instructions regarding other mods in the list.
- All the compatibility steps are handled internally by KOTORModSync and the default instructions files provided here. An end user simply can select the mods they want to install in the left list, and any customizations if they like, and simply press 'Install All' to have everything installed automatically.
- This program has a built-in GUI editor and an installer packed into one. Modbuild creators can create instructions with little to no knowledge of the format and easily share them with end users. End users can install everything from the instruction file with a simple click of a button. Edit any instructions and verify the configuration with built-in tools. There's also some support to dry run an install.
- A flexible configuration editor and parser utilizing TOML syntax. This is very user-friendly and similar to INI which TSLPatcher already uses and most modders are used to.
- Create instructions files with complex dependency structures for multiple mods, and have end users install everything exactly according to the instructions created. No more manually copying/deleting files: KOTORModSync handles all of that for your end user.

## Usage
This section is entirely a WIP but I'll post basic info on some things that are not obvious in the app.
First and foremost the installer does NOT download all required mods, it requires that all mods are downloaded, unextracted, to the same folder. (known as the `<<modDirectory>>`)
You'll need to click 'set directories' and browse to your `<<kotorDirectory>>` and your `<<modDirectory>>`.
Once you do this, load an instructions file (or create one), then select the mods to be installed and press 'install all'.

## Creating instructions.
The installer parses the fields `InstallBefore`, `InstallAfter`, `Dependencies`, and `Restrictions` to define dependencies and incompatibilities.
See https://pastebin.com/7gML3zCJ for a quick explanation of those fields. See the examples for the `Ultimate Character Overhaul` and the `Handmaiden/Disciple Same-Gender Romance Mod` for the more complex examples.

## FAQ:
- Q: This app is saying 'failed to validate some components'...
- A: Check the logs/output window/console window, find the archive it's complaining about (it's highlighted bold in red). Either download the archive so kotormodsync can find it, or deselect the mod in the app so it won't be considered for install.
- Q: I get an error saying the holopatcher binary failed to execute. how to fix?
- A: The most common reason is your AV is blocking it, but check your log file for more information.
- Q: I've created my own mod, will you add it to the KOTORModSync project?
- A: Yes! [Please submit your instructions here.](https://github.com/th3w1zard1/KOTORModSync/discussions/categories/share-your-instructions) As long as your mod is SFW, I'll most likely add it to the list.
- Q: A mod in the instructions file is out of date, where do I report this?
- A: [Please notify me that mod has been updated here.](https://github.com/th3w1zard1/KOTORModSync/discussions/categories/mod-updates)

## Supported Platforms
KOTORModSync is a cross-platform 32-bit and 64-bit .NET application. It is compatible with the following operating systems:
- **Windows 7 and 8:** Compatible if running **.NET Framework 4.6.2** or **higher**. The NET6 version is NOT supported.
- **Windows 10 and 11:** Compatible with both the **.NET Framework 4.6.2** *and* the **NET6** builds.
- **Linux and Mac:** Compatible with the **NET6** x64 builds - choose one of the two that match your operating system.
Users do not need to download any additional runtimes: everything is self-contained within the application. Additionally, **.NET Framework 4.6.2** is preinstalled on Windows 7 and 8 or at least provided by Windows Updates.

### Linux/Mac
You may need additional X11 development libraries. In order to get this working on WSL, for example, I had to install the following packages:

`sudo apt install libsm6 libice6 libx11-dev libfontconfig1 libx11-6 libx11-xcb1 libxau6 libxcb1 libxdmcp6 libxcb-xkb1 libxcb-render0 libxcb-shm0 libxcb-xfixes0 libxcb-util1 libxcb-xinerama0 libxcb-randr0 libxcb-image0 libxcb-keysyms1 libxcb-sync1 libxcb-xtest0`

Then you can simply run like this in a terminal:

`./KOTORModSync`

If you run into problems with the Linux or Mac builds, please contact me and I'll happily get a fix ready for the next release build. 


## Build instructions
I'm honestly not sure what all you need, I was able to build and run it on both vs2019 and vs2022. From what I understand, the minimum build requirements are:
- **NET6 or .NET Framework 4.6.2 targeting platform and build tools.**
- **.NET Standard Development Kit.**

All you need to do is build KOTORModSync.GUI. This should build the program into ./KOTORModSync.GUI/bin directory. Or run the command `dotnet build` then `dotnet run` inside KOTORModSync.GUI folder.
You may alternatively run my publish scripts in the solution directory if you like.
#### KOTORModSync.GUI
- Main application
- Uses AvaloniaUI v11 for the UI.
- Buttons, windows, dialogs, and controls defined here.
#### KOTORModSync.Core
- All the main logic is defined here.
- Targets .net standard 2.0
#### KOTORModSync.ConsoleApp
- Contains a few quickly written developer tools used to quickly test some features.


## Credit
#### Snigaroo
This man saved me countless amount of hours. I'd still be struggling through game glitches and mod-specific instructions I didn't understand. Actually, I'd probably still be on Dantooine trying to determine why I'm getting visual glitches and crashes which he solved with the one-word message 'grass'.

#### JCarter426
Ditto. There were so many KOTOR-specific things to learn, without his help I'd still be trying to deserialize encapsulated resource files. His time and patience were incredibly useful to the project and this project would be impossible without him.

#### Cortisol
Created HoloPatcher and the PyKotor library that KOTORModSync uses to patch mods. These projects are the main reason KOTORModSync can be supported on Mac/Linux. While the PyKotor/HoloPatcher projects have had some issues, this guy was more or less available for comment if I had questions on how I could fix any remaining problems myself.

### Testers:
##### Lewok from r/KOTOR
Thank you for helping test that obnoxious UAC elevation problem legacy Windows apps like TSLPatcher have.
##### Thor110
Tested multiple installs and provided wisdom on the internal workings of KOTOR.

#### Other notable users:
#### Fair-Strides
Provided the Perl source code of TSLPatcher on GitHub, and generally maintained the TSLPatcher project in Stoffe's absence.

#### *Stoffe*
Creator of *TSLPatcher*

Thank you to the entire KOTOR community for what you do.
