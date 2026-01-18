# Developer Guide

This guide is for mod developers who want to ensure their mods are compatible with Rooster's auto-discovery and installation systems.

## How to Name Your Mod

Rooster uses a point-based scoring system to link installed plugins to Thunderstore packages. A mod must accumulate **at least 60 points** to be successfully discovered.

### Scoring Rules

-   **Namespace + Name Match**: `+100 points`
    -   *Criterion*: Your GUID contains both the Thunderstore Author and the Package Name.
    -   *Example*: Package `Author-ModName` vs GUID `com.Author.ModName`.
-   **Exact GUID-to-Name Match**: `+80 points`
    -   *Criterion*: Your GUID (normalized) exactly matches the Thunderstore Package Name (normalized).
    -   *Example*: Package `SuperJump` vs GUID `com.whatever.SuperJump`.
-   **Token Match**: `+65 points`
    -   *Criterion*: All "tokens" in the Thunderstore name (e.g., words in PascalCase) are present in your Plugin Name.
-   **Partial GUID Match (Length Dependent)**:
    -   *Criterion*: Your GUID contains the Package Name.
    -   *Score*: `+65 points` if the name is 12+ characters, otherwise `+50 points`.
-   **Plugin Name Match**: `+60 points`
    -   *Criterion*: Your internal `BepInPlugin` Name exactly matches the Thunderstore Package Name.

### Recommended: Strict Naming
The most reliable way to ensure a match is to include both the **Author Name** and **Mod Name** in your GUID.

-   **Thunderstore Package**: `MyName-CoolMod`
-   **Your Plugin GUID**: `com.MyName.CoolMod`
-   **Score**: 100 points (Guaranteed Discovery)

### Caution: Short Generic Names
If your mod name is short and only appears partially in the GUID, it might fail the threshold.

-   **Thunderstore Package**: `Utils`
-   **Your Plugin GUID**: `com.internal.utils`
-   **Score**: 50 points (Ignored - Below 60 threshold)
-   **Fix**: Ensure the `BepInPlugin` name is also `Utils` (+60) or use a more unique name (+65).

## Why isn't my mod detected?
If a mod shows up in the list but doesn't have a green `*` indicator, Rooster detected the file but couldn't verify which online package it belongs to.
This usually happens because the mod's internal **Name** or **GUID** is too different from the **Thunderstore Name**.

## Mod Structure
 
Rooster supports three types of ZIP structures for installation and automatically handles path changes and folder unwrapping.

### 1. Standard (Plugin-Based)
The most common structure. The `.dll` file is located at the root of the ZIP (alongside `manifest.json`).

*   **Structure**: `ZIP Root` -> `MyMod.dll`, `manifest.json`
*   **Install Location**: `BepInEx/plugins/` (If a specific folder exists for the mod, it installs there).
*   **Features**:
    *   **Auto-Unwrap**: If your ZIP puts the DLL inside a folder (e.g. `MyMod/MyMod.dll`) that matches your install folder, Rooster prevents nesting (no `MyMod/MyMod/MyMod.dll`). 
    *   **Anti-Duplicate**: If you rename your plugin file or folder in an update, Rooster detects the path change and cleans up the old file to prevent duplicates.

### 2. Root-Based (Game Root)
Used for mods that need to place files in the game's root directory or multiple subdirectories.

*   **Structure**: `ZIP Root` -> `BepInEx/`, `manifest.json`
*   **Install Location**: Merged into `Ultimate Chicken Horse/`.
*   **Best For**: Complex mods, mod loaders, or mods replacing core game files.

### 3. BepInEx-Based (Plugins/Config Root)
Used for mods that explicitly target the `plugins` or `config` folders but are zipped without the parent `BepInEx` folder.

*   **Structure**: `ZIP Root` -> `plugins/` OR `config/`, `manifest.json`
*   **Install Location**: Merged into `Ultimate Chicken Horse/BepInEx/`.
*   **Best For**: Mods offering preset configurations or specific plugin organizations.

