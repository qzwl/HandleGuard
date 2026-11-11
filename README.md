# HandleGuard · 句柄 / 线程治理台

一个 Windows 原生小工具：定位“某个软件 bug 把句柄表或线程数撑爆”的那只进程，并按风险等级处置。

单文件 exe，无第三方依赖，.NET Framework 4.x + 全自绘 WinForms 界面（深/浅两套主题）。

协议：[Apache-2.0](LICENSE)（可自由使用、修改、商用；引用与商用请按 [NOTICE](NOTICE) 注明来源与仓库链接）。

---

## 先说清楚一件事：句柄不能“凭空清理”

Windows 的句柄是**进程私有的内核对象引用**，第三方程序没有合法途径“删掉别人的句柄”。网上那些“一键清理句柄”多半是改注册表、清缓存之类的擦边球。

所以这个工具做的是三件事：

1. **定位**：谁在涨、涨的是什么对象、涨了多少线程；
2. **温和释放**：修剪工作集、清空系统文件缓存、清空待机内存列表（能立刻缓解“整机卡”）；
3. **对症处置**：重启那个进程（最彻底），或强制结束、挂起失控线程、关闭确认重复的远端句柄。

---

## 能力清单

| 区域 | 能力 |
| --- | --- |
| 系统总览 | 全系统句柄总数、线程总数、进程数、物理内存、CPU（刷新间隔可调：1 / 5 / 15 秒 / 手动） |
| 进程排行 | 句柄 / 线程 / 工作集 / CPU / **Δ句柄** / **Δ线程**（相对稳定基线）；增长超阈值自动标黄、标红 |
| **排序** | 顶部五个预设（句柄总量 / 句柄增长 / 线程增长 / CPU / 内存），外加**点表头按任意列排序**；再点一次升/降序互换，表头带 ▲▼ 指示 |
| **进程档案** | 选中进程后一屏看清：映像路径、完整命令行、**父进程（谁把它拉起来的）+ 父进程路径**、**它又拉起了哪些下级进程**、启动时间与运行时长、运行账户、架构、文件说明/产品/公司/版本 |
| **文件与网络** | 该进程加载的 **75+ 个模块（DLL 全路径）**、它**正在打开的磁盘文件**、它的 **TCP/UDP 连接与状态** |
| 句柄明细 | 句柄值、类型（Event / Key / Section / Mutant…）、对象名、访问掩码、**同对象句柄 ×N** |
| 线程明细 | TID、CPU 时间、优先级、起始地址、线程描述（`.NET TP Worker` / `.NET Finalizer` 等） |
| 内存治理 | 一键修剪工作集 + 清文件缓存 + 清待机内存列表；也可单独执行后两项；**执行完会弹出结果框**，逐条说明成功项、失败原因与被归还的内存量 |
| 进程处置 | 结束选中进程、重启选中进程（推荐）、导出 Markdown 诊断报告 |
| 线程处置 | 挂起 / 恢复单个线程 |
| 句柄处置 | 关闭选中远端句柄（注入 `CloseHandle`，逐条确认） |

明细区四个页签：**进程档案 / 文件与网络 / 句柄明细 / 线程明细**，切换页签即读对应数据；「进程档案」页可一键把整份档案复制到剪贴板，「文件与网络」页可复制选中条目的完整路径。**没选中进程时四个页签统一显示“在上方列表里选一个进程”的空状态提示**，不会停在“正在读取…”这种看起来像卡住的文案上。

**界面**：左上角名称区（logo + HandleGuard）点一下弹出「关于」窗口——作者 qzwl、版本、技术栈、开源组件与许可清单、风险说明，可一键复制成文本；整条标题栏其余区域双击可最大化 / 再双击还原（最大化态也有效），拖动可移窗。

以 Edge WebView2 为例：`msedgewebview2.exe` 会显示成“由 XXX.exe (PID N) 启动”，点开“文件与网络”就能看到它加载的 DLL、打开的缓存文件和连出去的地址，再往上选父进程 XXX.exe，就是真正的调用方。

界面：无系统边框、自绘标题栏与圆角卡片、表格行悬停/选中态、列右对齐数字、DPI 按 `DeviceDpi` 手动缩放。

---

## 使用方法

双击 `build\HandleGuard.exe`（会请求管理员权限——枚举句柄表与处置动作需要）。

1. 看列表默认按 **句柄增长** 排序，找出“只增不减”的进程；想按别的维度看，直接**点表头**（PID / 进程 / 句柄 / 线程 / 工作集 / CPU / Δ句柄 / Δ线程），点第二次反向；
2. 列表多久刷一次由右上角「刷新 X 秒」决定：点一下循环 1 秒 → 5 秒 → 15 秒 → 手动；“手动”档下只有点「立即刷新」或做完处置才会重采。Δ 阈值会按刷新间隔等比放大，15 秒档不会因为窗口变长而全员标红；
3. 选中它 → 下方「进程档案」先看它是**谁拉起来的**（父进程）、**它自己在跑什么命令**（命令行）、**它又拉起了谁**（下级进程）；
4. 切到「文件与网络」看它加载的模块、打开的文件、连出去的地址；
5. 再看「句柄明细」有没有同对象 `×N`（典型泄漏：同一个 Event/Key 被反复打开）、「线程明细」线程数是否异常；
6. 处置优先级：**重启选中进程** ＞ 强制结束 ＞ 挂起线程；
7. 整机卡但找不到元凶时，先点「一键释放内存与缓存」。

状态栏会提示当前权限级别，以及“读不到类型/详情”的原因。

---

## 风险与边界（请务必看）

- **关闭远端句柄**：本质是向目标进程注入一次 `CloseHandle`。目标进程可能随后崩溃或丢数据。工具只允许关闭被判定为“同对象重复”的句柄，并且每次都弹确认框。用前先保存目标程序里的工作。
- **挂起线程**：如果挂到持锁线程上，进程会僵死。仅建议用来临时压住 CPU 空转的失控线程，用完及时恢复。
- **修剪工作集**：会让其他程序重新从磁盘加载，短暂卡一下是正常现象，不是清理失败。
- **非管理员运行**：`System`、`svchost` 等受保护进程的映像路径、命令行、句柄类型、线程详情读不到，会显示 `—`，状态栏与档案页会说明原因。以管理员运行则全部可读。
- **命令行**要读目标进程的 PEB（`NtQueryInformationProcess` + `ReadProcessMemory`），同样受权限限制；32 位（WOW64）进程按 x86 布局单独处理，不会读错偏移。
- **文件列表**只列**磁盘上的文件**：管道、设备这类句柄故意跳过（对同步命名管道查名字会卡住枚举线程）。因此列表是“打开的文件”而非“所有 IO 句柄”。
- **网络列表**覆盖 IPv4 的 TCP（含状态：LISTEN / ESTABLISHED / TIME_WAIT 等）与 UDP。
- **32 位目标进程**：不支持从 64 位程序跨架构关闭其句柄（会直接拒绝，不冒险）。
- **实测环境的限制**：非管理员时系统会把句柄的内核对象地址清零，此时“同对象句柄”退化为按 **类型 + 对象名** 统计（未命名对象不参与统计）；以管理员运行则按真实内核对象地址分组，判定更准。

### 为什么“管理员模式”下也可能无法清缓存（已修）

`SetSystemFileCacheSize` 需要 **提高配额特权**（`SeIncreaseQuotaPrivilege`），
`NtSetSystemInformation(MemoryPurgeStandbyList)` 需要 **性能诊断特权**（`SeProfileSingleProcessPrivilege`）。

管理员令牌里这两项特权是**存在但默认禁用**的，不显式 `AdjustTokenPrivileges` 启用，调用就会返回 `1314 ERROR_PRIVILEGE_NOT_HELD`——
旧版本把这种失败一律写成“需要管理员权限”，于是和左下角的“管理员模式”自相矛盾。现在的处理是：

- 每次清理前先启用对应特权（`Win.EnablePrivilege`）；
- 失败时弹窗给出**真实错误码**（5 = 拒绝访问 / 1314 = 缺少特权 / 1300 = 未被分配 / NTSTATUS），并区分“特权没能启用（可能被组策略限制）”与“真的不是管理员”；
- 不再出现“管理员模式下提示需要管理员权限”这种说法。

---

## 技术实现要点

- 句柄表：`NtQuerySystemInformation(SystemExtendedHandleInformation = 64)`，x64/x86 双布局偏移；缓冲区按 `retLen` 自动扩容（本机约 9 MB / 22 万条）。
- 类型与对象名：`DuplicateHandle` 拿到本进程副本后 `NtQueryObject`，**类型名按 ObjectTypeIndex 缓存**；只对白名单类型查对象名（避开 `File`/管道类句柄查名会阻塞的坑）。
- 系统总量：本机 `GetPerformanceInfo` 返回 `ERROR_BAD_LENGTH(24)`（结构长度校验不通过），因此改为**逐进程 `HandleCount` / 线程数汇总**，与列表数据自洽。
- 关闭远端句柄：`CreateRemoteThread` 以 `kernel32!CloseHandle` 为入口、句柄值作参数，等 3 秒读返回值。
- 清缓存：`SetSystemFileCacheSize(-1,-1)`、`NtSetSystemInformation(SystemMemoryListInformation, MemoryPurgeStandbyList)`。
- 线程：Toolhelp32 + `GetThreadTimes` + `NtQueryInformationThread(ThreadQuerySetWin32StartAddress)` + `GetThreadDescription`。
- 进程档案：`QueryFullProcessImageName`（路径）、`NtQueryInformationProcess(ProcessBasicInformation)` + 读 PEB 取命令行、PPID 反查父子关系、`FileVersionInfo` 取版本信息、`OpenProcessToken` + `LookupAccountSid` 取账户。
- 模块：Toolhelp32 `Module32First/Next`；文件：全表句柄 → `DuplicateHandle` → `GetFileType` 只认 `FILE_TYPE_DISK` → `GetFinalPathNameByHandle` 转 DOS 路径；网络：`GetExtendedTcpTable` / `GetExtendedUdpTable`（OWNER_PID 表）。
- DPI：`SetProcessDpiAwarenessContext(PER_MONITOR_V2)`，布局与自绘尺寸按 `DeviceDpi / 96` 手动换算。
- 图标：`Brand.cs` 是唯一的品牌几何（256 基准的画布，纯蓝渐变圆角方块 + 白 H + 右侧弧，H 与弧的包围盒在中轴上、左右各留 40/256），
  `Brand.Draw` 等比缩放到任意尺寸，16/24px 走一套按比例收窄的小尺寸几何让弧仍然可见；
  `make-icon.ps1` 把它编成 `build\icon-shot.exe`，生成 16/20/24/32/40/48/64 的 DIB 条目 + 128/256 的 PNG 条目，`/win32icon` 嵌入 exe，
  运行时再用 `Icon.ExtractAssociatedIcon` 让任务栏 / Alt+Tab 复用同一图标。标题栏左上角 logo 调的是同一个 `Brand.Draw`。
- 自绘一致性：卡片、色条、表格这类“方角压在圆角上”的地方，统一用圆角路径 `SetClip` 后再画（否则方角会在圆角外侧戳出一小块底色）；
  描边色 `Border` / `BorderSoft` 提高了对比度，浅色 `#C2CDDC`、深色 `#38465A`，按钮描边统一用 `Border`。
- 状态栏：左侧权限段 + 中部系统指标 + 右侧操作消息；消息超过可用宽度时改为**跑马灯横向滚动**（提前 MeasureText 判断，不滚动时右对齐）。
- 排序：`DataGrid` 自己算列宽、做表头命中，点表头回调列索引（`HeaderClicked`），排序与 ▲▼ 指示都在 `MainForm.OnHeaderSort` / `CmpCol` 里做；用顶部预设排序时清掉表头排序列，两套排序不会打架。
- 刷新：`System.Windows.Forms.Timer` 的间隔直接取档位值（1/5/15 秒，0 = 手动）；Δt 阈值按 `间隔 / 1.4 秒` 等比放大，保证“每分钟涨 20 个句柄”在任何档位都是同一个颜色。
- 双击标题栏：无边框窗口拿不到系统自带的双击最大化，`WM_NCHITTEST` 必须在**最大化态**也返回 `HTCAPTION`，再由自己拦 `WM_NCLBUTTONDBLCLK` 切状态；还原时把 `s_restoreBounds` 显式写回，避免无边框窗口还原后尺寸漂移。
- 编译：C# 5（系统自带 `csc.exe`），全部代码不依赖 C# 6+ 语法。

---

## 目录

```
HandleGuard\
├─ src\
│  ├─ Brand.cs          **品牌几何唯一来源**（蓝色圆角方块 + 白 H + 右侧弧），exe 图标与界面 logo 都调它
│  ├─ IconShot.cs       图标生成器，把 Brand 渲染成多尺寸 ico
│  ├─ Native.cs         Win32/NT 调用、句柄与线程枚举、处置动作、令牌特权启用
│  ├─ Inspector.cs      进程全息档案：命令行、父子关系、模块、文件、网络
│  ├─ Theme.cs          配色、字体、自绘卡片/按钮/列表/分段控件
│  ├─ ConfirmDlg.cs     自绘确认 / 结果对话框
│  ├─ AboutDlg.cs       自绘「关于」窗口（作者 / 版本 / 技术栈 / 开源组件 / 风险说明）
│  ├─ MainForm.cs       主界面、采样循环、交互
│  ├─ Program.cs        入口、DPI、离屏快照自测
│  └─ app.manifest      提权与 DPI 清单
├─ build.ps1            一键编译（产物在 build\）
├─ make-icon.ps1        图标生成（Brand.cs → src\app.ico）
├─ LICENSE              Apache License 2.0 全文
├─ NOTICE               归属与署名要求
└─ README.md
```

仓库里不含编译产物与 `src\app.ico`：图标由 `make-icon.ps1` 从 `Brand.cs` 现场生成，exe 由 `build.ps1` 编译，两条命令就能复现出完全一样的成品。

## 换图标

图标不是一张图改出来的，而是 `src\Brand.cs` 里的几何：**纯蓝竖向渐变的圆角方块 + 白色 H + 右侧一道白色弧**。
改 `Brand.cs` 里的常量（圆角、笔画宽度、弧半径与角度），然后：

```powershell
.\make-icon.ps1    # 编出 build\icon-shot.exe，渲染 16~256 共 9 个尺寸写入 src\app.ico
.\build.ps1        # 编译时用 /win32icon 嵌入 exe
```

小尺寸（16/24px）另有一套按比例收窄的几何：H 变细让出右侧 1px 给弧线、弧加粗到至少 1px，
否则那条弧会和 H 粘成一团白斑；26px 以上用主几何（H 与小弧的包围盒正好落在画布中轴，左右留白各 40/256）。
exe、资源管理器、任务栏、Alt+Tab 和窗口左上角 logo 用的是同一份几何。

## 编译

```powershell
.\make-icon.ps1    # 首次需要：从 Brand.cs 生成 src\app.ico
.\build.ps1        # 编出 build\HandleGuard.exe 与 build\HandleGuard-test.exe
```

只要 Windows 自带的 .NET Framework 4.x（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`），**不需要装 SDK、不需要 Visual Studio**。
`build.ps1` 会检查图标是否存在，缺了会提示先跑 `make-icon.ps1`。

## 自测方式

```powershell
.\build\HandleGuard-test.exe --snapshot .\shots
```

窗口移到屏幕外，依次切换四个明细页、深/浅两套主题、弹一次确认框和一次清理结果框、开一次「关于」窗口、清空选中核对空状态、点一次表头排序，输出 13 张 PNG 并写 `selftest.log`（进程数、句柄/线程总量、刷新档位、排序状态、选中进程 PID 与各页条数、状态栏文案），全程约 33 秒、退出码 0，不占用桌面。

> 前台直接 `& exe --snapshot` 会被 15 秒等待切到后台、只出一张图；用 `Start-Process -PassThru -Wait` 跑才稳定。

想复现“上次用浅色主题时的首次启动画面”（验证圆角处不再有黑块），把 `%LOCALAPPDATA%\HandleGuard\theme.txt` 写成 `light` 再设环境变量 `HG_KEEP_THEME=1` 跑一次即可。

---

## 授权

本项目以 **Apache License 2.0** 授权，全文见 [LICENSE](LICENSE)。你可以自由地使用、修改、分发、商用，包括闭源产品中集成。

附加署名要求（见 [NOTICE](NOTICE)）：

1. 保留 `NOTICE` 文件全文；
2. 引用代码、二次分发或商业使用时，注明来源与仓库链接 **https://github.com/qzwl/HandleGuard**；
3. 修改过的版本，注明“基于 HandleGuard 修改”。

作者：周木木（qzwl）· https://github.com/qzwl

---

## 免责

这是个直接操作内核句柄、线程和内存列表的工具，用错了能搞崩目标程序。请先读懂「风险与边界」，重要工作先保存。软件按 Apache-2.0 第 7、8 条“按现状提供”，作者不承担使用后果。
