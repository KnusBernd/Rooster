# Rooster 🐓

A BepInEx mod manager for **Ultimate Chicken Horse** that keeps your mods up to date.

## Features

- **Auto-Discovery** — Matches installed mods to Thunderstore packages using smart heuristics
- **One-Click Updates** — Download and install updates from the main menu
- **GitHub Support** — Install curated mods hosted directly on GitHub
- **Ignore Updates** — Hide notifications for mods you don't want to update
- **Auto-Update** — Optionally enable automatic updates per mod
- **Hot Swap** — Updates are staged and applied on next game restart
- **Welcome Disclaimer** — One-time popup explaining risks and usage

## Installation

1. Install [BepInEx 5.x](https://thunderstore.io/c/ultimate-chicken-horse/p/BepInEx/BepInExPack/)
2. Download the latest `Rooster.dll`
3. Copy it to `BepInEx/plugins/` in your game folder
4. Launch the game. You should see a "Checking for Mod Updates..." message in the lower right corner, a disclaimer popup and a Mod Menu button on the main menu.

> [!TIP]
> **First-Time Installation Note**: When you first install Rooster, you might see update notifications for mods you are sure are already current. This usually happens because developers sometimes forget to update the version number inside the mod file itself. Simply let Rooster perform the update once—this allows it to sync the versions correctly. You won't see these notifications again until a genuine new update is uploaded by the developer!

## How It Works

On game startup, Rooster performs the following steps:

1. **Fetches Package List**: Downloads the complete list of UCH mods from Thunderstore and the curated GitHub list.
2. **Scans Installed Plugins**: Reads the metadata (`BepInPlugin`) from all `.dll` files in your plugins folder.
3. **Matches Packages**: Uses a specific scoring system to link your local files to online packages.
4. **Checks Versions**: Compares your installed version against the latest online version.
5. **Notifies**: If updates are found, a popup appears.

## Security & Privacy

Rooster takes security seriously to ensure your game and computer remain safe while modding.

-   **Trusted Source**: Rooster **only** connects to the official [Thunderstore API](https://thunderstore.io/) and GitHub. It never communicates with third-party or unknown servers.
-   **Data Privacy**: The connection is **one-way**. Rooster downloads the public mod list from Thunderstore/GitHub. It **never** uploads your installed mods, usage data, or personal information.
-   **Integrity Verification**: Currently, Rooster relies on HTTPS transport security. Thunderstore mods are approved by the Thunderstore community. GitHub mods are curated by the Rooster team.    
-   **Open Source**: The full source code is available for audit, ensuring transparency in how your mods are managed.
-   **User Control**: You are in charge. You can ignore specific mods or disable auto-updates entirely if you prefer manual management.

## System Requirements & Limitations

- **Internet Connection**: A stable internet connection is required to fetch the mod list and download updates. If the connection fails, Rooster fails silently.
- **Thunderstore API**: This mod relies entirely on the Thunderstore API. If Thunderstore is down or changes its API structure, updates will not work.
- **OS Support**: This mod is designed and tested for **Windows**. 
    - The "Hot Swap" mechanism is a workaround for Windows file locking.
    - Linux/macOS support is **experimental/untested** (though the file operations should theoretically work).

## Known Issues
- None currently known.

## For Developers

If you're a mod developer, see [DEVELOPERS.md](https://github.com/KnusBernd/Rooster/blob/master/DEVELOPERS.md) for guidance on:
- Naming your mod for auto-discovery
- Structuring your ZIP package for proper installation
- Troubleshooting detection issues

## Support

If you experience issues, have questions, or want to contribute, join the **UCH Mods Discord Server**:
[https://discord.gg/WeUtumaqaj](https://discord.gg/WeUtumaqaj)

Alternatively, you can file an issue or pull request on the [GitHub repository](https://github.com/KnusBernd/Rooster).

## License

MIT License
