# ElectromagneticPulseGenerator

A RimWorld mod that adds an Electromagnetic Pulse Generator building.

## Features
- Scans for ore deposits
- Custom building and job definitions
- Configurable scan timing in the mod settings

## Installation
1. Clone or download this repository.
2. Place it in your RimWorld Mods folder.

## Scan settings
Open the mod settings to adjust the mean time between finds and the guaranteed scan limit. Both values can be adjusted with the slider or entered directly in ticks, from 60 ticks (1 second) to 600,000 ticks (10 days). These settings apply to both the powered scanner and the Save Our Ship 2 passive scanner. Korean translations are included.

When auto-mining a revealed deposit, the scanner reuses existing mining designations where possible and chooses a straight route that adds the fewest new mineable tiles.

### Build

On Windows, run the repository-root `Build.bat` to build both supported versions. The compiled DLLs are copied to `1.6/Assemblies` and `1.7/Assemblies`. To build only one version, run `Source/1.6/Build.bat` or `Source/1.7/Build.bat`.

The scripts detect the common Steam install paths, including `Y:\SteamLibrary\steamapps\common\RimWorld`. For a custom location, set the `RIMWORLD_DIR` environment variable or pass the game folder as the first argument. Install the .NET SDK and the .NET Framework 4.7.2 targeting pack. Close RimWorld before building so the game does not lock the output DLL.

## License
GPL-3.0 license
