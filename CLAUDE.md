# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Unity **6000.6.5f1** 2D URP game: a clicker (click the Command Core/HQ for credits, buildings earn passively) on an
isometric base-building grid (place / move / upgrade / sell), with flat, angular, red/navy/cream art.
Target is PC/Web landscape, mouse-driven. See README.md for player-facing controls.

## Commands

Play: open `Assets/Scenes/SampleScene.unity` in Unity and press Play. No scene setup is needed (see Bootstrap below).

Tests (NUnit, EditMode, Core rules only) — in the editor: Window → General → Test Runner. From a terminal:

```bash
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```

```bash
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter "StartupClicker.Tests.CoreTests.Upgrade_DoublesOutput_AndStopsAtMax"
```

Regenerate art / sound (Python 3 + Pillow; outputs go straight into `Assets/Resources`):

```bash
python3 Tools/art/generate_art.py --preview
```

```bash
python3 Tools/art/generate_sfx.py
```

`--preview` also writes `Tools/art/preview.jpg` (mock scene) and `Tools/art/sheet.jpg` (every building × level) —
view these to check art changes without Unity.

**Without a Unity editor** (e.g. cloud sessions): `StartupClicker.Core` + the tests have no Unity dependency, so they
can be compiled with Roslyn (`Microsoft.Net.Compilers.Toolset` from NuGet) against a .NET 8 runtime and run with
NUnitLite. The `Game` layer can only be type-checked against Unity reference DLLs from NuGet
(`UnityEngine.Modules`, `Unity3D.UnityEngine.UI`) plus a hand-written stub for the Input System — say so when
reporting results, since that is not the same as compiling in Unity 6.

## Architecture

Two assemblies with a strict split:

- **`Assets/Scripts/Core`** (`StartupClicker.Core.asmdef`, `noEngineReferences: true`) — all game rules in plain C#.
  - `Catalog.cs` is the single source of balance data (buildings, upgrades, grid size, cost growth, offline/frenzy constants).
  - `GameSim` is the **only** thing that mutates `GameState`. Every action is a `Try*` method that returns null/false/-1
    on failure, then calls `Recalculate()` to rebuild cached multipliers and total CPS. `Tick(dt)` adds passive income.
  - `GameState` is the save DTO: public fields only so Unity's `JsonUtility` serialises it. `GameSim`'s constructor runs
    `Sanitise()` so corrupt/outdated saves are repaired (the HQ is always restored) instead of crashing.
  - The HQ (`Catalog.CoreId = "core"`) is an ordinary `PlacedBuilding` with 0 CPS; it can be moved but `TrySell` refuses it.
- **`Assets/Scripts/Game`** (`StartupClicker.Game.asmdef`) — Unity view/controller layer; reads the sim, never owns rules.
  - `Bootstrap` uses `[RuntimeInitializeOnLoadMethod]` and `sceneLoaded` to create a `GameController`, which builds the
    camera rig, `WorldView` and `Hud` entirely in code. There are no prefabs and the scene is effectively empty — don't
    add scene/prefab YAML wiring.
  - `GameController` owns the `GameSim`, handles input, placement/move modes, supply drops, income pops and saving
    (JSON in `PlayerPrefs`, key `startupclicker.save.v1`; autosave every 15 s and on quit/pause/focus loss). It raises
    `Changed` (shop needs rebuilding) and `SelectionChanged`.
  - `Hud` builds all uGUI in code via the `Ui` helpers (`Ui.Img/Label/Btn/Place/Stretch`). Uses legacy uGUI `Text`
    with the bundled Barlow Condensed font — **not TextMeshPro** (avoids the TMP Essentials import step).

### Isometric conventions (spread across several files — keep consistent)

- `IsoGrid`: 2:1 diamond; +x goes down-right on screen, +y down-left; 1 tile = 1 world unit wide; 256 PPU. Grid centre maps to world (0, 0).
- Draw order is `IsoGrid.Order(gx, gy) = round((gx + gy) × 10)` using the footprint centre; ground tiles, highlights
  and FX use fixed sorting-order bands defined in `WorldView`.
- A building's `(X, Y)` is its lowest-coordinate cell; its sprite pivot is the **front corner** of the footprint
  (`IsoGrid.FootprintFront`).
- That pivot relies on `PAD_B = 16` in `Tools/art/iso.py` matching `BuildingBottomPad` in `SpriteBank.cs`. Change both or neither.
- Click picking (`WorldView.Pick`) tests real sprite pixels via `SpriteBank.Mask` (alpha copied once through a
  RenderTexture, so textures need not be CPU-readable), front-most sorting order wins, then falls back to grid occupancy.

### Assets are loaded by name, not by import settings

`SpriteBank` loads PNGs with `Resources.Load<Texture2D>("Art/<name>")` and builds `Sprite`s in code (pivot, PPU, 9-slice
borders), so regenerated art needs no `.meta` edits. Naming: buildings are `b_<defId>_<level>` (levels 1–5), UI is
`ui_*`, icons `icon_*`, effects `fx_*`; sounds are `Resources/Audio/<name>.wav` played via `Sfx.Play(name)`.

**Adding a building** = add a `BuildingDef` to `Catalog.Buildings` + a recipe function with the same id in
`generate_art.py`'s `BUILDINGS` dict, then regenerate art. Mk II/III upgrades are generated automatically per building.

## Input model

- Left-click the HQ earns credits; this fires on press, not release, so rapid clicking feels instant.
- Left-click also collects supply drops and confirms place/move.
- Left-click on any other building only calls `OnBuildingPrimary`, a hook reserved for future building abilities.
- Right-click (without dragging) selects any building, HQ included, for Upgrade / Move / Sell, or cancels the current mode.
- Dragging with any mouse button pans the view. Mouse wheel zooms.
- The project uses the **new Input System only** (`activeInputHandler: 1`): use `UnityEngine.InputSystem` (`Mouse.current`, `Keyboard.current`), never `UnityEngine.Input`. UI uses `InputSystemUIInputModule`.

## Repo notes

- Root `.gitattributes` routes png/wav/ttf/jpg to Git LFS. `Assets/Resources/{Art,Audio,Fonts}` and `Tools/art` each
  contain a `.gitattributes` that opts those files **out** of LFS (they're small and regenerable), because pushing LFS
  objects from cloud sessions is blocked.
- Commit the `.meta` files Unity generates for new assets.
