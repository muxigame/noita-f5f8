# Noita F5 / F8

[English](README.md) | [简体中文](README.zh-CN.md)

Windows 10/11 的 Noita 完整快存与读档模组。[Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3835902155) · [GitHub](https://github.com/muxigame/noita-f5f8)

## 安装

必须安装为**本地不安全模组**。任选一种来源：

1. **创意工坊**：订阅后，将 `Steam/steamapps/workshop/content/881100/3835902155` 整个文件夹复制到游戏安装目录的 `mods` 下。
2. **本仓库**：新建 `Noita/mods/3835902155`，将仓库中 `mod/noita_f5f8/` 的**全部内容**复制进去。

两种方式的最终结构都必须是 `Noita/mods/3835902155/mod.xml`。物理文件夹名必须为 `3835902155`，不要再套一层 `noita_f5f8`。

在 Noita 模组菜单允许不安全模组，启用本地副本，停用同名工坊副本，按游戏提示重启。此后照常启动游戏：本地模组验证随附助手的 SHA-256 后自动启动助手，正常退出游戏后助手也会退出。无需单独运行助手或设置 Steam 启动选项。只订阅工坊副本无法启用存读档。

更新时先退出游戏，再用新版覆盖整个本地文件夹；本地副本不会自动跟随工坊更新。

## 使用与数据

- **F5**：请求游戏正常保存退出 → 校验并保存完整快照 → 重启。
- **F8**：先校验快照 → 立即强制停止游戏 → 恢复整个 `save00` → 重启。没有有效快照时不会关闭游戏。
- F5/F8 重启后，在 Windows 稳定版 **Build Jan 25 2025** 自动继续；未知版本需手动点“继续”。首次普通启动保留主菜单。

仅支持普通 `save00` 存档，恢复会回退其中全部进度、世界、角色与统计；`save_shared` 设置不变。按键仅在 Noita 位于前台时响应。保持一个游戏实例，等待助手完成复制并重启，期间不要再手动打开游戏。助手不弹出 Windows 通知。

快照、历史与日志位于 `%LOCALAPPDATA%/NoitaF5F8/`；当前快照是 `quick/save00`，校验清单是 `quick/manifest.xml`。读档前的恢复副本位于 `<存档根目录>/NoitaF5F8-Recovery/`。手动恢复该副本时先退出游戏，再用选定 `before-load-*` 文件夹的全部内容替换 `<存档根目录>/save00/`。状态与错误可在助手托盘菜单及 `helper.log` 查看。

## 构建

使用 Windows 自带的 .NET Framework C# 编译器，无需下载包。在仓库根目录运行：

```powershell
powershell -File .\companion\build.ps1 -Test
powershell -File .\build-release.ps1 -SkipBuild
```

仓库包含完整 C# 源码，以及可直接安装的 `NoitaF5F8.bin` 和 `SHA256.txt`。第二步会更新模组中的助手载荷、源码副本和哈希，并生成 `dist/Noita-F5F8-Local.zip`。

## 许可证

项目代码采用 [MIT 许可证](LICENSE.txt)。Noita 和 Steam 界面截图展示的是相应权利人的软件。
