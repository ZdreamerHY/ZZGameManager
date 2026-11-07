# ZZ游戏管理器

轻量 Windows 本地游戏管理器，支持手动添加、封面、启动、打开目录、名称排序、最近启动、搜索、编辑、路径修复与拖动节点自定义配色。

## 运行成品

完整解压 `release/ZZ游戏管理器-v1.2.zip`，双击其中的 `ZZGameManager.exe`。数据在 exe 旁的 `data` 目录，第一次运行自动创建。请使用可写目录。运行需要 .NET Framework 4.8 或更高的 4.x 版本；本机已验证 4.8.1。

同目录提供《使用手册》《设计文档》《验收记录》的 HTML 与 Markdown 版本。

## 从源码构建

在 Windows PowerShell 中运行：

```powershell
.\build.ps1
.\test.ps1
```

主程序使用系统 .NET Framework C# 编译器与标准库，无第三方运行依赖，不需要 NuGet 或 npm。已生成的图标与 HTML 文档均随源码提供。

如果修改了图标或 Markdown 文档，可使用 Python 与 Pillow 重新生成资源：

```powershell
python .\tools\generate_assets.py
.\build.ps1
```

`package.ps1` 重新构建并生成成品 ZIP 与源码 ZIP。它不会把现有游戏库 data 目录打包。

测试证据位于 `tests/runs/时间戳/results.txt`。其中的游戏名和探针 exe 仅用于隔离测试，成品没有预置真实游戏记录。

## 结构

- `src/`：程序与图标资源。
- `tests/`：独立测试代码、运行结果和测试夹具。
- `docs/`：使用手册、设计文档与验收记录。
- `tools/`：可选的文档与图标生成脚本。
- `release/`：可运行成品与 ZIP 交付包。

第一版不包括自动扫描、标签、云同步、游玩时长与存档管理。实现和验证边界详见设计文档与验收记录。
