# Realistic Crime, Healthcare and Fire Burden - Build Instructions

## Prerequisites
- Install **.NET SDK 8.0 x64** (required, not just runtime): https://dotnet.microsoft.com/download/dotnet/8.0
  - Verify: `dotnet --info` shows `SDKs installed: 8.0.x`
- Install **.NET 6.0 Runtime x64** (for `ModPostProcessor.exe`)
- Install **Modding Toolchain** in-game: `Cities Skylines II -> Options -> Modding` -> enable toolchain, let it install Unity 2022.3.71f1 + Entities 1.3.10 + Burst.
  - This sets env vars `CSII_TOOLPATH`, `CSII_MANAGEDPATH`, etc. Restart PC after install.
- No live game files are modified. Source at `C:\FRACTIS\Dev\Fractis process\RealisticCrimeSystem`, output goes to `%LOCALAPPDATA%\Colossal Order\Cities Skylines II\Mods\RealisticCrimeSystem` automatically via `Mod.targets`.
- Exclusive target: **1.6.0f1**, no backward compat. Close the game before building (DLL lock -> MSB3231).

## Structure
```
RealisticCrimeSystem/  (assembly name kept; display name is "Realistic Crime, Healthcare and Fire Burden")
  RealisticCrimeSystem.csproj  (net48, Version 1.0.0, + Colossal.Collections for happiness params)
  Mod.cs              -> IMod OnLoad registers 9 systems at GameSimulation
  Setting.cs          -> Balanced/Medium/Full presets (Full default) + 6 absolute Tuning sliders
  LocaleEN.cs
  Systems/
    DynamicCrimeSystem.cs       -> all difficulty writes: tolerance, welfare factor, crimeMod,
                                   occurrence, catch window, sentencing, fire, happiness (self-healing)
    CrimeResponseSystem.cs      -> outcome audit only (arrest/escape/hospital/rehab). Creates nothing.
    OverdoseSystem.cs           -> sick-wave medical events, civilians only (never criminals)
    RehabSystem.cs              -> hospital retention loading beds
    VandalismSystem.cs          -> escape fallout deteriorates high-crime buildings
    PrisonBalanceSystem.cs      -> jail/prison occupancy + capacity observer (infoview method)
    CrimeStatsReporterSystem.cs -> stats/wave/alert chirps via soft Custom Chirps bridge + capacity warnings
    CustomCrimeDataSystem.cs    -> one-shot CrimeData diag + extras report (never deletes)
    FirePressureSystem.cs       -> one-shot FireData baseline observer
  Bridge/CustomChirpsBridge.cs  -> soft reflection bridge (silent without Custom Chirps)
  Properties/
    PublishConfiguration.xml  -> Paradox Mods metadata (display name, descriptions, 1.0.0, 1.6.0f1)
    Thumbnail.png
```

## Build
```powershell
dotnet build "C:\FRACTIS\Dev\Fractis process\RealisticCrimeSystem\RealisticCrimeSystem.csproj" -c Release
# Expect: Build succeeded, 0 Warning(s), 0 Error(s). Deploy copies to Mods\RealisticCrimeSystem.
# Launch game, enable mod in Options -> Mods. Set preset Full (default), sliders snap per preset.
```

## Design rules (do not regress)
- Vanilla systems only: tune prefabs/singletons/configs, never replace behavior, never create dispatch/crime entities.
- Every write is absolute-from-baseline + sanity-banded + drift-checked (self-healing, no stacking).
- Overdose/sick systems must never target Criminal entities (ambulances steal arrests).
- CustomCrimeData must never delete entities (order not guaranteed).
- Balanced restores vanilla values exactly (verified in logs).

## Publishing (Paradox Mods only, uploader does this)
1. Login to PDX account in-game.
2. Publish from the toolchain with `Properties/PublishConfiguration.xml` (ModId empty on first upload).
3. Upload package = `Mods\RealisticCrimeSystem` contents (dll, pdb, win/linux/mac binaries) + description + screenshots.

## Spec
Full spec: `../REALISTIC_CRIME_SYSTEM.md`
