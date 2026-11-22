# Noita F5 / F8

[English](README.md) | [简体中文](README.zh-CN.md)

Full-run quicksave and load for Noita on Windows 10/11. [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3835902155) · [Source code](https://github.com/muxigame/noita-f5f8)

## Installation

Install as a **local unsafe mod** using either source:

1. **Workshop:** subscribe, then copy the entire `Steam/steamapps/workshop/content/881100/3835902155` folder into the game's `mods` directory.
2. **This repository:** create `Noita/mods/3835902155` and copy **all contents** of `mod/noita_f5f8/` into it.

Both methods must produce `Noita/mods/3835902155/mod.xml`. The physical folder name must be `3835902155`, without an extra `noita_f5f8` level.

Allow unsafe mods in Noita, enable the local copy, disable the Workshop copy, and restart when prompted. The local mod verifies the bundled helper's SHA-256 and starts it automatically. Normal game exit closes the helper. No separate helper launch or Steam Launch Options are needed. Subscribing alone does not enable saving/loading.

To update, exit the game and replace the complete local folder. The local copy does not update automatically with Workshop.

## Controls and data

- **F5:** request normal save-and-quit → verify and commit a full snapshot → restart.
- **F8:** validate the snapshot first → immediately terminate the game → restore the entire `save00` → restart. An invalid or missing snapshot leaves the game running.
- Restarts automatically Continue on Windows stable **Build Jan 25 2025**. Unknown builds require manual Continue. Initial normal startup retains the main menu.

Only the standard `save00` is supported. Restoring rolls back all progress, world state, character state and statistics within it; `save_shared` settings remain unchanged. Hotkeys work only while Noita is in the foreground. Keep one game instance running and wait for the helper to finish copying and restart it. The helper does not show Windows notification toasts.

Snapshots, history and logs are in `%LOCALAPPDATA%/NoitaF5F8/`. The current snapshot is `quick/save00`, with checksums in `quick/manifest.xml`. Before-load recovery copies are in `<save root>/NoitaF5F8-Recovery/`. To restore one manually, exit Noita, then replace `<save root>/save00/` with the contents of the selected `before-load-*` directory. Status and errors are available from the helper's tray menu and `helper.log`.

## Build

Uses the installed Windows .NET Framework C# compiler, with no package downloads. Run from the repository root:

```powershell
powershell -File .\companion\build.ps1 -Test
powershell -File .\build-release.ps1 -SkipBuild
```

The repository includes the complete C# source and an installable `NoitaF5F8.bin` with `SHA256.txt`. The second command refreshes the bundled helper, source copy and checksum, and creates `dist/Noita-F5F8-Local.zip`.

## License

Project code is licensed under the [MIT License](LICENSE.txt). Noita and Steam interface screenshots depict their respective owners' software.
