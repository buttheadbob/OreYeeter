# OreYeeter

Players keep their own list of ores that their ship drills void as they mine, so the ore is never created and never
fills a container. Built for two plugin loaders from one code base:

| Build | Loader | Project | Patching | Config |
|---|---|---|---|---|
| Torch | Torch for the Dedicated Server | `TorchPlugin` (.NET Framework 4.8) | Torch's native `[PatchShim]` (no Harmony) | WPF tab + `OreYeeterConfig.cfg` |
| Magnetar | Magnetar (Legacy .NET 4.8, Interim .NET 10), managed by Quasar | `ServerPlugin` (`net48;net10.0`) | Harmony | Quasar renders it from the PluginSdk attributes |

## Layout
```
Shared/          engine neutral core, compiled into both plugins (game assemblies only)
  OreYeeterCore.cs   host/config interfaces, ore list parsing and matching
  DrillLogic.cs      the decision made by the TryHarvestOreMaterial prefix
  OreActions.cs      behaviour of every !ore command, returns the reply text
  PlayerOreStore.cs  per player json files   GridUtils.cs   look-at grid lookup
TorchPlugin/     Torch host: plugin class, WPF config UI, [Category("ore")] commands, native patch
ServerPlugin/    Magnetar host: IPlugin, PluginSdk config + [CommandRoot("ore")] commands, Harmony patch
OreYeeter.xml    MagnetarHub descriptor (fill in the TODOs when publishing)
Build/           OreYeeter.zip for Torch, written by the Torch build
```
Rules for `Shared/`: no Torch, NLog, Harmony or PluginSdk references, C# 8 syntax at most (the Torch project is limited
to it), and talk to the loader only through `IOreYeeterHost` / `IOreConfig`. Magnetar compiles the `ServerPlugin` and
`Shared` directories itself, so anything else in them must also compile there. New behaviour belongs in `Shared`, the
host projects stay thin wrappers.

## Building
1. Run `Setup (run before opening solution).bat` once. It creates the `GameBinaries` / `TorchBinaries` junctions (Torch
   build) and `Directory.Build.props` from the template (Magnetar build: `Dedicated64`, `Magnetar`, `MagnetarBin`).
2. Run `build.bat` (`build.bat torch`, `build.bat magnetar`, optional `debug` / `nodeploy`), or open `OreYeeter.sln`, or `msbuild OreYeeter.sln -p:Configuration=Release`. Do not use plain `dotnet build` on the
   solution, the Torch project needs the .NET Framework MSBuild. `dotnet build ServerPlugin -c Release` works on its own.
   - Torch: `Build\OreYeeter.zip` (dll + `manifest.xml`) after every build.
   - Magnetar: `ServerPlugin\bin\Release\net48` and `net10.0`. The build also copies the dll to
     `%AppData%\Magnetar\<Legacy|Interim>\Local` when that folder exists (`-p:DeployToMagnetar=false` disables it).
     `PluginSdk.dll` comes from the Magnetar install, or from a build of the Magnetar repository's `PluginSdk`
     (set `MagnetarBin` in `Directory.Build.props`).
3. Versions: `Version.Build.props` (Magnetar) and `TorchPlugin\manifest.xml` (Torch), bump both.

## How it works
- The prefix on `MyDrillBase.TryHarvestOreMaterial` returns false to void the ore. Both loaders bind prefix parameters
  by **name** only, so they must match the original's (`__instance`, `material`, `removedAmount`).
- A drill yeets when its name contains the tag (default `!ore`) or its owner ran `!ore auto`.
- A tagged or auto drill voids ores on the server list (`MasterOres`) or its owner's personal list. Untagged drills void nothing, not even server list ores.
- Personal lists: `PlayerOres\<steamid>.json` under the plugin's storage folder. Players without a file use `DefaultPlayerOres`, and follow later changes to it.
- On a cluster (Magnetar) the storage folder is the PluginSdk's shared directory, the same on every node, so a player's
  list follows them to whichever node they play on. Every node reads the files again within 5 seconds of a change.
  A cluster without shared storage (several hosts with no shared mount) logs a warning and keeps the lists per node.
  `!ore master add` / `remove` would change one node's config only, so they refuse there: set `MasterOres` in Quasar.

## Commands
| Command | Who | |
|---|---|---|
| `!ore list` / `add <ore>` / `remove <ore>` | player | manage your list |
| `!ore auto` | player | toggle yeeting on all your ship drills |
| `!ore clean` | player | remove listed ores already stored on the grid you sit in or look at |
| `!ore master list` / `add` / `remove` | admin | server wide list, players cannot remove these |
| `!ore reload` | admin (Torch only) | reload the config. On Magnetar edit it in Quasar |

## License
Public domain ([The Unlicense](LICENSE)): anyone may copy, modify, use and redistribute it for any purpose, no conditions.
