#Valheim Mod development on Linux

Since there's little to nothing written about how to set up the infrastructure for modding on Linux, here's the rough steps I used.  this is for Ubuntu 22.04, so change specific package install commands as required.

## Basic Steps

1. Install dotnet command line tool: `apt install dotnet`
2. Install dotnet SDK: `apt install dotnet-sdk-6.0`
3. Publicize the Valheim objects so that they can be referenced:
 - get the BepInEx publicizer: https://github.com/elliotttate/Bepinex-Tools/releases/tag/1.0.1-Publicizer
 - Drop into BepInEx plugins directory like any other mod; run Valheim once to generate the publicized assemblies
 - publicized assemblies will be in the Valheim directory, not the mod instance directory if you're using r2modman or similar.
4. Build with `dotnet build`


## Notes
The error `Could not load file or assembly 'System.Security.Permissions, Version=4.0.3.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'. The system cannot find the file specified.` actually means "You have a syntax error in your project XML, such as an unmatched tag."
