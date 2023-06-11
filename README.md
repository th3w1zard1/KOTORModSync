# KOTORModSync
KOTORModSync is a multi-mod installer for KOTOR games that makes it easier to install and manage mods. I usually install the Snigaroo modbuild every year or so. The last time I did so I made a mistake on a single different step 3 times in a row and had to start over each time. So I decided to build a tool to simplify the process.

![image](https://github.com/th3w1zard1/KOTORModSync/assets/2219836/d7871e62-a27d-4864-b5bb-d5f002141ac3)



## Goals
Mod creators work really hard on their mods. It's the least we can do to install them and use them, right? However who wants to reinstall to vanilla just to spend 4 hours reinstalling mods, simply to add 1 or 2 extra mods on top of it?
Enter KOTOR MODSync.
The main goal of this project is to not be another dead installer that requires significant changes to a hard-to-understand configuration file. In order to do that, KOTORModSync has a built-in GUI editor which will parse and load the TOML instruction file. End users can install everything from an instruction file with a simple click of a button.
Will include SHA1 validation to prevent errors during installation.

## Features
A flexible configuration editor and parser utilizing TOML syntax. This is very similar to INI which TSLPatcher already uses and most modders are used to.
Create instructions files with complex dependency structures for multiple mods, and have end users install everything exactly according to your instructions. No more manually copying/deleting files: KOTORModSync handles all of that for your end user.
Supports file validation using hashes of a game install to ensure each mod installed correctly. In order for this to work, this assumes the user started with a vanilla install.

## Platforms
KOTORModSync is compatible with Windows7-11, Linux, and Mac, as well as anything supported by .NET Standard 2.0.


### Linux/Mac
Only the x64 version is supported on Linux/Mac, all you need is .net 6.0 or higher. You may need additional X11 development libraries. In order to get this working on WSL I had to install the following packages:

`sudo apt install libsm6 libice6 libx11-dev libfontconfig1 libx11-6 libx11-xcb1 libxau6 libxcb1 libxdmcp6 libxcb-xkb1 libxcb-render0 libxcb-shm0 libxcb-xfixes0 libxcb-util1 libxcb-xinerama0 libxcb-randr0 libxcb-image0 libxcb-keysyms1 libxcb-sync1 libxcb-xtest0`

Then you can simply run the EXE like this in a terminal:

`./KOTORModSync.exe`
