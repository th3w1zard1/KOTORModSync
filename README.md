# KOTORModSync
KOTORModSync is a multi-mod installer for KOTOR games that makes it easier to install and manage mods. I usually install the Snigaroo modbuild every year or so. The last time I did so I made a mistake on a single different step 3 times in a row and had to start over each time. So I decided to build a tool to simplify the process.

## Features
A flexible configuration editor and parser utilizing TOML syntax, which is very similar to INI.
Create instructions files with complex dependency structures and share them with anyone.
This also supports file validation using hashes of a game install to ensure mods installed correctly. In order for this to work, this assumes the user started with a vanilla install.

![image](https://github.com/th3w1zard1/KOTORModSync/assets/2219836/094af450-d300-4db5-82be-5614e6dea78e)


## Goals
The main goal of this project is to not be some dead attempt that requires significant changes to a hard-to-understand configuration file. In order to do that, KOTORModSync can create and load a configuration file which provides instructions and specific mods dynamically. It also will include SHA1 validation to prevent errors during installation.

## Platforms
KOTORModSync is compatible with Windows7-11, Ubuntu, and Mac, as well as anything supported by .NET Standard 2.0.
