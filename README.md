# Rooster 🐓

A BepInEx mod manager for **Ultimate Chicken Horse** that keeps your mods up to date.


> [!WARNING]
> **BETA STATUS**: Rooster is in active development. Features are experimental. Backing up your `BepInEx/plugins` folder is recommended to keep your setup safe while testing.

## Features

- **Auto-Discovery** — Matches installed mods to Thunderstore packages using smart heuristics
- **One-Click Updates** — Download and install updates from the main menu
- **Ignore Updates** — Hide notifications for mods you don't want to update
- **Auto-Update** — Optionally enable automatic updates per mod
- **Hot Swap** — Updates are staged and applied on next game restart
- **Beta Warning** — Startup popup warning users of experimental status (can be disabled)

## Installation

1. Install [BepInEx 5.x](https://thunderstore.io/c/ultimate-chicken-horse/p/BepInEx/BepInExPack/)
2. Download the latest `Rooster.dll`
3. Copy it to `BepInEx/plugins/` in your game folder
4. Launch the game. You should see a "Rooster Beta Warning" popup on the main menu.

## How It Works

On game startup, Rooster performs the following steps:

1. **Fetches Package List**: Downloads the complete list of UCH mods from the Thunderstore API.
2. **Scans Installed Plugins**: Reads the metadata (`BepInPlugin`) from all `.dll` files in your plugins folder.
3. **Matches Packages**: Uses a specific scoring system to link your local files to online packages.
4. **Checks Versions**: Compares your installed version against the latest online version.
5. **Notifies**: If updates are found, a popup appears.


## Security & Privacy

Rooster takes security seriously to ensure your game and computer remain safe while modding.

-   **Trusted Source**: Rooster **only** connects to the official [Thunderstore API](https://thunderstore.io/). It never communicates with third-party or unknown servers.
-   **Data Privacy**: The connection is **one-way**. Rooster downloads the public mod list from Thunderstore. It **never** uploads your installed mods, usage data, or personal information.
-   **Integrity Verification** *(Planned)*: Future versions will verify downloads against SHA256 hashes when Thunderstore API support is available.
-   **Open Source**: The full source code is available for audit, ensuring transparency in how your mods are managed.
-   **User Control**: You are in charge. You can ignore specific mods or disable auto-updates entirely if you prefer manual management.

## System Requirements & Limitations

- **Internet Connection**: A stable internet connection is required to fetch the mod list and download updates. If the connection fails, Rooster fails silently.
- **Thunderstore API**: This mod relies entirely on the Thunderstore API. If Thunderstore is down or changes its API structure, updates will not work.
- **OS Support**: This mod is designed and tested for **Windows**. 
    - The "Hot Swap" mechanism is a workaround for Windows file locking.
    - Linux/macOS support is **experimental/untested** (though the file operations should theoretically work).

## Known Issues
- **Game Freeze on Download**: The game may freeze briefly while downloading larger mods because the download happens on the main thread. UCH mods tend to be small (<1MB) so this should not be an issue for most users.

## For Developers

If you're a mod developer, see [DEVELOPERS.md](https://github.com/KnusBernd/Rooster/blob/master/DEVELOPERS.md) for guidance on:
- Naming your mod for auto-discovery
- Structuring your ZIP package for proper installation
- Troubleshooting detection issues

## Roadmap & Todo & Nice to have
- [ ] **Async Downloads**: Move downloading to a background thread to prevent game freezing
- [ ] **Config Editor**: Reuse the UCH Tablet UI to create a full BepInEx configuration editor (replacing the need for configuration manager)
- [ ] **RSA Code Signing**: Implement RSA key verification to allow developers to sign their mods, ensuring only trusted versions are installed
- [ ] **GitHub Integration**: Fetch and display metadata from GitHub, including detailed release notes, changelogs, and issue trackers for open-source mods
- [ ] **Scalability Optimization**: Improve API fetching to handle large mod lists efficiently (e.g. pagination or caching) instead of fetching all at once

## Support

If you experience issues, have questions, or want to contribute, join the **UCH Mods Discord Server**:
[https://discord.gg/WeUtumaqaj](https://discord.gg/WeUtumaqaj)

Alternatively, you can file an issue or pull request on the [GitHub repository](https://github.com/KnusBernd/Rooster).

## License

MIT License
