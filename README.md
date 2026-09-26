# 内存卫士 · Memory Guardian

**维护者：宋域强（GitHub：[@syq10086](https://github.com/syq10086)）** · 公众号 / 抖音：宋域强

[下载安装包](https://github.com/syq10086/MemoryGuardian/releases/latest) · [项目源码](https://github.com/syq10086/MemoryGuardian) · [作者主页](https://blog.csdn.net/syq10086?type=blog)

**0.6.0 更新**：软件更名为“内存卫士”，保留 3D 地球。低占用为蓝色，50% 以上逐渐变暖，95% 及以上为红色；读取失败保留最后一次颜色。右键菜单和“关于”新增“更新与下载”，指向 `https://www.xiaopuwa.com/earth-guardian/`。开发者、公众号、抖音：宋域强。保留原有自动清理规则及倒计时。退出旧版后再安装新版。

内部 EXE 仍为 `EarthGuardian.exe`，沿用安装标识、配置目录和登录任务，方便升级并保留设置。公开下载包使用 `MemoryGuardian-0.6.0-*` 名称。网站位于 `website/earth-guardian/`；部署见 `website/DEPLOY.md`，上传包由 `scripts/package-site.ps1` 生成。

一个只显示悬浮球的 Windows 内存清理工具。基于 [Windows Memory Cleaner](https://github.com/IgorMundstein/WinMemoryCleaner) 二次开发，使用 **GNU GPL v3**。

<p align="center"><img src="docs/images/earth-guardian.png" width="270" alt="内存卫士 3D 地球悬浮球" /></p>

## 使用

### 安装版（推荐）

运行 `MemoryGuardian-0.6.0-Setup.exe`，按中文向导选择安装目录，保留默认勾选的“创建桌面快捷方式”。安装完成后，桌面及开始菜单会出现“内存卫士”。也可在完成页面勾选立即运行。

安装包默认需要管理员授权，安装到 Program Files。应用本身清理内存也需要管理员权限。安装前会检查 .NET Framework 4.8.1；缺少时提示从微软官网下载，不会隐式安装其他组件。安装不会强制开启登录启动。

卸载入口位于 Windows 设置 → 应用 → 内存卫士，以及开始菜单 → 内存卫士 → 卸载内存卫士。卸载移除程序及安装创建的快捷方式，保留 `%LOCALAPPDATA%\MemoryOrb` 中的设置。指向本安装目录的 `MemoryOrb-<SID>` 登录启动任务会尝试移除；不删除其他安装或免安装版本的任务。

由免安装版切换时请先退出旧版；如原来开启登录启动，在安装版中关闭再开启一次来更新程序路径。安装包尚未做数字签名。

### 免安装版

支持 Windows 10 / 11，需 .NET Framework 4.8.1（本次在 Windows 11 上构建并检查）。下载并解压发布包后，双击 `EarthGuardian.exe`，由用户在 Windows 提示中授予管理员权限。程序未做数字签名。

- **单击**：执行七项完整清理，完成后显示系统可用内存变化。
- **拖动**：移动悬浮球；保存位置并在下次恢复，支持拖到副屏。
- **右键**：立即优化、自动优化、置顶、大小、登录启动、错误信息、作者主页、开源许可、退出。
- **托盘图标单击**：将球找回主屏；右键也能打开菜单。
- **键盘**：球获得焦点后，空格或回车触发清理，Shift+F10 打开菜单。

默认球体直径约 72 DIP，可选 56 / 72 / 88 DIP（高 DPI 屏幕对应更多物理像素）。采用 WPF 三维球体、Natural Earth 海陆纹理、方向光和大气光晕。默认朝向亚洲，优化时以 24 fps 旋转，结束后恢复默认视角；平时停止动画，每两秒刷新内存数据。

## 清理范围和结果

0.4.0 手动清理按原项目各区域的顺序执行以下七项：

1. 进程工作集：`MemoryEmptyWorkingSets`（命令 2）。
2. 系统文件缓存：`SetSystemFileCacheSize(-1, -1, 0)`。
3. 已修改页面：`MemoryFlushModifiedList`（命令 3）。
4. 全部待机缓存：`MemoryPurgeStandbyList`（命令 4）。
5. 合并页面：`SystemCombinePhysicalMemoryInformation`（类 130）。
6. 注册表缓存：`SystemRegistryReconciliationInformation`（类 155）。
7. 固定磁盘写入缓存：逐卷调用 `FlushFileBuffers`，记录无法访问或刷新失败的卷。

完整模式使用全部待机缓存选项，不重复运行其子集“低优先级缓存”。右键“仅清理低优先级缓存”只执行命令 5。自动模式根据用户选择的强度执行。

范围对应上游的完整区域选择，底层实现有两处区别：系统文件缓存仅使用上游也调用的公开 API，不重复使用 NT 文件缓存结构接口；磁盘只刷新待写数据，不调用上游可选的写入顺序重置及缓存丢弃 IOCTL。不会终止进程或删除文件；完整清理可能短暂卡顿和产生磁盘写入，不保证清理量或性能与上游一致。

工作集整理可能降低任务管理器的“使用中”内存，但并不解除程序的内存分配；程序再次访问数据时占用会回升，也可能产生页错误并短暂变慢。不是每台机器、每次操作都能看到明显下降。

右键“查看上次优化结果”显示当前运行中的最近结果；报告保存在 `%LOCALAPPDATA%\MemoryOrb\last-optimization.txt`。报告逐项记录完成或失败，部分失败不会被标成全部成功。该文件每次覆盖，不累积日志。

球上的百分比来自 `GlobalMemoryStatusEx`。结果显示的是操作前后“可用物理内存”的差值，会受其他程序同时活动影响，不是准确归因的释放量，更不代表速度提升。待机缓存原本也可能被 Windows 计入可用内存，因此清理成功后仍可能显示“无明显变化”。Windows 接口失败时会显示失败提示，可在右键菜单查看具体原因。

手动操作进行中不能重复触发，完成后有 5 秒冷却。

## 自动清理：复选框与滑块

1. 右键地球，勾选 **启用自动清理**；取消勾选后不再自动触发（已经开始的系统操作会执行完）。
2. 打开 **自动清理设置**，用鼠标拖动阈值滑块，可调范围为 **60%～95%**，每格 1%。点击滑轨或用方向键也能调整，实时显示百分比并保存。
3. 设置最短间隔：5、10、15 或 30 分钟，推荐 10 分钟。
4. 选择强度：**均衡（推荐）**整理工作集和低优先级缓存；温和只清理低优先级缓存；完整执行七项清理，可能带来更多卡顿或磁盘写入。

新安装默认开启自动清理：**80%，持续 30 秒，间隔 10 分钟，均衡模式**。升级保留旧版的开关状态；旧版已关闭时，需要手动勾选。设置与位置一起保存，下次启动恢复。

每两秒读取占用，高占用持续 30 秒才触发，短暂峰值不会触发。低于阈值、读取失败、暂停或休眠造成超过 8 秒的采样间断时，会重新观察。手动及自动清理尝试都会开启自动冷却（失败也计入）；改变设置不会清空冷却时间。

地球低于阈值时显示“自动守护”，持续超标时显示“XX秒后清理”，冷却时显示“冷却 X分”（向上取整）。右键主菜单直接显示精确到秒的剩余时间和上次清理时间；悬停提示说明当前状态、自动强度和触发规则。关闭自动模式显示“手动模式”。报告标注手动或自动触发。

例如设置 60%，当前占用 67%：没有冷却时连续观察 30 秒后自动清理；如果刚手动或自动清理过，则等待所选间隔结束，且仍需满足持续超标条件。可在“自动清理设置”将最短间隔调为 5 分钟，或单击地球立即清理（手动仍有 5 秒防重复间隔）。改变阈值或重新勾选不会清空冷却。软件不保证防止所有卡顿，也不能替代处理内存泄漏或增加物理内存。

设置：`%LOCALAPPDATA%\MemoryOrb\settings.xml`。最近错误：同目录下的 `last-error.txt`。不读取或修改原版 WinMemoryCleaner 的设置，不自动更新到原作者版本。

登录启动默认关闭。用户在右键菜单开启时，才创建仅当前用户登录触发的 `MemoryOrb-<用户 SID>` 计划任务。移动 EXE 后请重新设置；删除软件前请先关闭登录启动。

## 构建

构建安装包：安装 Inno Setup 6.4.3 和应用构建依赖后运行：

```powershell
./scripts/build-installer.ps1 -Dotnet 'C:\path\to\dotnet.exe' -Iscc 'C:\path\to\ISCC.exe'
```

输出为 `artifacts/installer/MemoryGuardian-0.6.0-Setup.exe`。安装脚本和中文语言文件位于 `installer/`，均包含在源码包中。中文语言文件来自 Inno Setup 官方源码仓库，原译者信息保留在文件头。

安装 .NET SDK 8 和 .NET Framework 4.8.1 Developer Pack（或包含它的 Visual Studio Build Tools）。

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

产物位于 `artifacts/earth-release-0.6.0/EarthGuardian.exe`。运行时依赖 Windows 的 .NET Framework 4.8.1，不需要安装 .NET 8 运行时。该项目不含第三方运行时库。

指定 SDK：

```powershell
./scripts/build.ps1 -Dotnet 'C:\path\to\dotnet.exe'
```

不申请管理员权限的界面演示构建：

```powershell
./scripts/build.ps1 -Preview
./artifacts/earth-preview-0.6.0/EarthGuardian.exe --demo
```

`--demo` 读取真实内存占用，但点击仅播放动画，会明确显示“演示完成 · 未执行清理”；不保存设置、不修改登录启动、不执行自动清理。预览构建仅供测试；正式发布请用正常构建。

## 验证

```powershell
dotnet build tests/MemoryOrb.Tests.csproj -c Release
./tests/bin/Release/net481/MemoryOrb.Tests.exe
```

无第三方测试框架的检查程序覆盖自动清理阈值与冷却、并发保护、错误恢复、设置读写及损坏恢复、真实 Windows 内存读取、WPF 透明窗口、菜单、演示异步流程、动画结束和内嵌许可证。

检查程序包含 60 项检查。管理员权限下的真实清理、计划任务启停以及不同缩放比例的多显示器组合，需要在发布前进行人工实机验收；这些行为不能仅凭界面演示认定通过。详见 [验收记录](docs/VALIDATION.md)。

## 作者主页与版本 0.2.0

右键菜单和关于窗口均有“作者主页”，点击后通过默认浏览器打开 [syq10086 的 CSDN 博客](https://blog.csdn.net/syq10086?type=blog)。

为兼容旧版设置，内部命名空间、设置目录和计划任务标识仍保留 MemoryOrb。EXE 文件名为 EarthGuardian.exe，窗口、托盘及产品属性显示“内存卫士”。退出旧版后再运行新版；如开启过登录启动，请在新版中重新设置以更新路径。

海陆数据采用公有领域 Natural Earth 数据，来源见 NOTICE.md。运行 `scripts/create-earth-assets.ps1` 可重建纹理和图标。

## 源码结构

- `orb/`：新的悬浮球应用、设置和内存清理适配。
- `src/`：保留的上游源码；新程序直接引用其中的 Windows API 声明和内存结构。
- `tests/`：行为与 WPF 冒烟检查。
- `scripts/`：构建脚本。
- `.github/workflows/memory-orb.yml`：GitHub Actions 构建和测试。
- `docs/upstream/`：原 README 和原 GitHub 配置归档，避免运行原项目发布流程。

## 发布到 GitHub

可以发布本项目及修改后的源码，也可以按 GPL v3 收费分发。

1. 建立自己的 GitHub 仓库，将本目录的源码上传，遵守 `.gitignore`，不上传 `bin/`、`obj/` 或本机设置。
2. 保留 `LICENSE`、`NOTICE.md` 和原作者的版权/许可信息；继续以 GPL v3 许可整个衍生程序，标明你的修改。
3. 创建版本标签，并为 EXE 的**同一个版本**提供完整对应源码、构建脚本及许可证。
4. Release 可以附加二进制 ZIP 和源码 ZIP。不要把它描述为原作者的官方版本。

用户有权按 GPL 修改、重新编译和再分发。仅提供上游链接不能代替提供你实际修改版本的对应源码。许可证和署名已经整理在 [NOTICE.md](NOTICE.md) 中；在公开发布前可再补充你的项目主页和维护者名称。

本仓库由原项目 3.0.8 源码修订 `a19f25719c9b9a58a0acea99cb8adb4737d2d318` 创建。不是原作者官方版本，不使用原作者的发布签名。软件不提供担保，详见 [LICENSE](LICENSE)。
