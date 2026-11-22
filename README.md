# Noita F5 / F8

Windows 10/11 的 Noita 完整快存与读档模组。[Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3835902155) · [GitHub](https://github.com/muxigame/noita-f5f8)

## 中文

### 安装

必须安装为**本地不安全模组**。任选一种来源：

1. **创意工坊**：订阅后，将 `Steam/steamapps/workshop/content/881100/3835902155` 整个文件夹复制到游戏安装目录的 `mods` 下。
2. **本仓库**：新建 `Noita/mods/3835902155`，将仓库中 `mod/noita_f5f8/` 的**全部内容**复制进去。

两种方式的最终结构都必须是 `Noita/mods/3835902155/mod.xml`。物理文件夹名必须为 `3835902155`，不要再套一层 `noita_f5f8`。

在 Noita 模组菜单允许不安全模组，启用本地副本，停用同名工坊副本，按游戏提示重启。此后照常启动游戏：本地模组验证随附助手的 SHA-256 后自动启动助手，正常退出游戏后助手也会退出。无需单独运行助手或设置 Steam 启动选项。只订阅工坊副本无法启用存读档。

更新时先退出游戏，再用新版覆盖整个本地文件夹；本地副本不会自动跟随工坊更新。

### 使用与数据

- **F5**：请求游戏正常保存退出 → 校验并保存完整快照 → 重启。
- **F8**：先校验快照 → 立即强制停止游戏 → 恢复整个 `save00` → 重启。没有有效快照时不会关闭游戏。
- F5/F8 重启后，在 Windows 稳定版 **Build Jan 25 2025** 自动继续；未知版本需手动点“继续”。首次普通启动保留主菜单。

仅支持普通 `save00` 存档，恢复会回退其中全部进度、世界、角色与统计；`save_shared` 设置不变。按键仅在 Noita 位于前台时响应。保持一个游戏实例，等待助手完成复制并重启，期间不要再手动打开游戏。助手不弹出 Windows 通知。

快照、历史与日志位于 `%LOCALAPPDATA%/NoitaF5F8/`；当前快照是 `quick/save00`，校验清单是 `quick/manifest.xml`。读档前的恢复副本位于 `<存档根目录>/NoitaF5F8-Recovery/`。手动恢复该副本时先退出游戏，再用选定 `before-load-*` 文件夹的全部内容替换 `<存档根目录>/save00/`。状态与错误可在助手托盘菜单及 `helper.log` 查看。

### 构建

使用 Windows 自带的 .NET Framework C# 编译器，无需下载包。在仓库根目录运行：

```powershell
powershell -File .\companion\build.ps1 -Test
powershell -File .\build-release.ps1 -SkipBuild
```

仓库包含完整 C# 源码，以及可直接安装的 `NoitaF5F8.bin` 和 `SHA256.txt`。第二步会更新模组中的助手载荷、源码副本和哈希，并生成 `dist/Noita-F5F8-Local.zip`。

## English

Full-run quicksave and load for Noita on Windows 10/11. [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3835902155) · [Source code](https://github.com/muxigame/noita-f5f8)

### Installation

Install as a **local unsafe mod** using either source:

1. **Workshop:** subscribe, then copy the entire `Steam/steamapps/workshop/content/881100/3835902155` folder into the game's `mods` directory.
2. **This repository:** create `Noita/mods/3835902155` and copy **all contents** of `mod/noita_f5f8/` into it.

Both methods must produce `Noita/mods/3835902155/mod.xml`. The physical folder name must be `3835902155`, without an extra `noita_f5f8` level.

Allow unsafe mods in Noita, enable the local copy, disable the Workshop copy, and restart when prompted. The local mod verifies the bundled helper's SHA-256 and starts it automatically. Normal game exit closes the helper. No separate helper launch or Steam Launch Options are needed. Subscribing alone does not enable saving/loading.

To update, exit the game and replace the complete local folder. The local copy does not update automatically with Workshop.

### Controls and data

- **F5:** request normal save-and-quit → verify and commit a full snapshot → restart.
- **F8:** validate the snapshot first → immediately terminate the game → restore the entire `save00` → restart. An invalid or missing snapshot leaves the game running.
- Restarts automatically Continue on Windows stable **Build Jan 25 2025**. Unknown builds require manual Continue. Initial normal startup retains the main menu.

Only the standard `save00` is supported. Restoring rolls back all progress, world state, character state and statistics within it; `save_shared` settings remain unchanged. Hotkeys work only while Noita is in the foreground. Keep one game instance running and wait for the helper to finish copying and restart it. The helper does not show Windows notification toasts.

Snapshots, history and logs are in `%LOCALAPPDATA%/NoitaF5F8/`. The current snapshot is `quick/save00`, with checksums in `quick/manifest.xml`. Before-load recovery copies are in `<save root>/NoitaF5F8-Recovery/`. To restore one manually, exit Noita, then replace `<save root>/save00/` with the contents of the selected `before-load-*` directory. Status and errors are available from the helper's tray menu and `helper.log`.

### Build

Uses the installed Windows .NET Framework C# compiler, with no package downloads. Run from the repository root:

```powershell
powershell -File .\companion\build.ps1 -Test
powershell -File .\build-release.ps1 -SkipBuild
```

The repository includes the complete C# source and an installable `NoitaF5F8.bin` with `SHA256.txt`. The second command refreshes the bundled helper, source copy and checksum, and creates `dist/Noita-F5F8-Local.zip`.

## License

Project code is licensed under the [MIT License](LICENSE.txt). Noita and Steam interface screenshots depict their respective owners' software.
