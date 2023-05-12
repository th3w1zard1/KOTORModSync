# KOTORModSync
A flexible multi-mod installer for KOTOR games.

I got tired of reinstalling all of these mods on the modbuild page one by one every few years, so one day after failing arbitrary steps late in the install process multiple times, I decided to build a C# program that will do it all and handle/validate the dependencies automatically.

This project is unfinished.
This program is aimed to install multiple mods with complex dependencies and provide a flexible and easy-to-manage configuration file.

## Goals:
Instructions/specific mods are loaded from a configuration file, which the program uses to determine what to install and how to install it precisely and accurately.
The program utilizes SHA1 validation to validate each instruction before completing another so the user can determine if they've made a mistake anywhere in the process.

## Target platforms
Windows7-11, ubuntu, and mac, anything supported by .net standard 2.0.
