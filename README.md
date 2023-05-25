# KOTORModSync
KOTORModSync is a multi-mod installer for KOTOR games that makes it easier to install and manage mods. I usually install the Snigaroo modbuild every year or so. The last time I did so I made a mistake on a single different step 3 times in a row and had to start over each time. So I decided to build a tool to simplify the process.

![image](https://github.com/th3w1zard1/KOTORModSync/assets/2219836/094af450-d300-4db5-82be-5614e6dea78e)


## Goals
Mod creators work really hard on their mods. It's the least we can do to install them and use them, right? However who wants to reinstall to vanilla just to spend 4 hours reinstalling mods, simply to add 1 or 2 extra mods on top of it?
Enter KOTOR MODSync.
The main goal of this project is to not be another dead installer that requires significant changes to a hard-to-understand configuration file. In order to do that, KOTORModSync has a built-in GUI editor which will parse and load the TOML instruction file.
Will include SHA1 validation to prevent errors during installation.

## Features
A flexible configuration editor and parser utilizing TOML syntax. This is very similar to INI which TSLPatcher already uses and most modders are used to.
Create instructions files with complex dependency structures for multiple mods, and share them with anyone.
Supports file validation using hashes of a game install to ensure each mod installed correctly. In order for this to work, this assumes the user started with a vanilla install.

## Platforms
KOTORModSync is compatible with Windows7-11, Linux, and Mac, as well as anything supported by .NET Standard 2.0.
