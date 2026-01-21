# Developer Guide
This guide is for mod developers who want to ensure their mods are compatible with Rooster's auto-discovery and installation systems.

## How to Name Your Mod
Rooster uses a point-based scoring system to link installed plugins to Thunderstore packages. Maybe a bit overkill but whatever. To be successfully discovered, a mod must accumulate **at least 60 points**.

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
-   **Fix**: Ensure the `BepInPlugin` name is also `Utils` (+70) or use a more unique name (+65).

### Scoring Rules
If strict naming isn't possible, here is a detailed breakdown of how points are awarded:

-   **Namespace + Name Match**: `+100 points`
    -   *Criterion*: Your GUID contains both the Thunderstore Author and the Package Name.
    -   *Example*: Package `Author-ModName` vs GUID `com.Author.ModName`.
-   **Exact GUID-to-Name Match**: `+80 points`
    -   *Criterion*: Your GUID (normalized) exactly matches the Thunderstore Package Name (normalized).
    -   *Example*: Package `SuperJump` vs GUID `com.whatever.SuperJump`.
-   **URL Repo Name Match**: `+70 points`
    -   *Criterion*: The repository name (last segment) **exactly matches** OR **contains** your plugin name (normalized, case-insensitive).
    -   *Condition*: For containment matches, your plugin name must be > 4 characters long.
    -   *Example*: URL `.../User/UCH-BestMod` matches Plugin Name `BestMod`.
-   **Exact Name Match**: `+70 points`
    -   *Criterion*: Your internal `BepInPlugin` Name exactly matches the normalized Thunderstore Package Name.
-   **Token Match**: `+65 points`
    -   *Criterion*: All "tokens" in the Thunderstore name (e.g., words in PascalCase) are present in your Plugin Name.
-   **Partial GUID Match (Length Dependent)**:
    -   *Criterion*: Your GUID contains the Package Name.
    -   *Score*: `+65 points` if the name is 12+ characters, otherwise `+50 points`.
-   **Fuzzy Prefix Match**: `+60 points`
    -   *Criterion*: Your plugin name shares a significant prefix with the Thunderstore name (e.g. `LevelTools` vs `LevelToolsMod`).
    
## GitHub Mods
Rooster supports installing mods directly from GitHub via a curated list. This is useful for mods that are not on Thunderstore or for testing development builds.

### Artifact Auto-Discovery
Rooster automatically scans your repository to find mod artifacts using the following strategies, in order of priority:

1.  **Release Assets**: Checks standard GitHub Releases for attached `.zip` or `.dll` files.
    - *Best for*: Standard releases.
2.  **Release Body Links**: Scans the release description text for direct `http/https` links to `.zip` files.
    - *Best for*: Hosting files externally (e.g., Google Drive, Discord) while using GitHub Releases for changelogs.
3.  **Source Tree Scan (Fallback)**: If no release assets are found, Rooster recursively scans the repository's `HEAD` file tree for any `.dll` or `.zip` files.
    - *Best for*: Simple mods that just commit the compiled DLL to the repo.
    - *Note*: Mods found this way are assigned version `1.0.0` by default.

### Matching Requirements
Files found via these strategies are converted into virtual packages. The **filename** determines the package name `(e.g., MyMod.dll -> MyMod)`. 
Ensure your artifact filename matches your `BepInPlugin` name to satisfy the [Auto-Discovery](#how-to-name-your-mod) rules explained above.

### Curated List
To be visible in Rooster, your repository must be added to the internal curated list. Submit a Pull Request to add your repo (User/Repo).

## Developer Verification Tool (Built-in)
Rooster includes a hidden developer tool to verify your mod's match score directly in-game.

### Enabling the Tool
1.  Open `BepInEx/config/de.knusbernd.rooster.cfg` (or use Configuration Manager).
2.  Set `DeveloperMode = true`.
3.  In-game, press **F3** to toggle the Developer UI.

### Features
-   **Live Inspector**: Click any running plugin to see a detailed **Match Report**, explaining exactly which rules triggered heavily.
-   **Match Simulator**: 
    -   Enter your **Local Mod Details** (GUID and Name).
    -   Enter your target **Thunderstore Details** (Date Package Name and optional Repo URL).
    -   **Click Simulate**: The tool will calculate the exact score your mod would get.
    -   *Ghost Text*: Use the gray placeholder text as default values to quickly test the example logic.

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

*   **Structure**: `ZIP Root` -> `plugins/`, `config/` OR `patchers/`, `manifest.json`
*   **Install Location**: Merged into `Ultimate Chicken Horse/BepInEx/`.
*   **Best For**: Mods offering preset configurations, specific plugin organizations, or preloader patchers.

### File Filtering
To keep your plugins folder clean, Rooster **excludes** the following metadata files during installation/updates:
*   `manifest.json`, `manifest.yml`
*   `icon.png`
*   `readme.md`, `changelog.md`

Any other files (dlls, assets, configs) are copied as-is.

## Versioning Mitigation
Rooster understands that developers sometimes use different version formats (e.g., `v1.0.0` vs `1.0.0`). To ensure updates are detected correctly, it applies the following logic:
-   **Strips Prefixes**: `v1.2` becomes `1.2`.
-   **Ignores Metadata**: Anything after a hyphen is ignored (`1.0.0-beta` -> `1.0.0`).
-   **Normalizes Padding**: `1.2` is treated as equal to `1.2.0`.
