# Native Guard 设计文档

日期：2026-07-04

## 目标

Native Guard 是一个面向 Windows ARM64 的托盘应用，用来帮助用户查看当前正在运行的桌面进程里，哪些是非原生进程，例如通过 Windows ARM64 仿真层运行的 x64 或 x86 进程。

第一版优先保证低开销，并只使用稳定的公开 Windows API。应用不会做周期性 CPU 采样，也不会估算能耗。

## 范围

Native Guard 会实现：

- 作为 WinUI 3 托盘应用运行在 Windows ARM64 上。
- 使用 .NET 10 和 C#。
- 发布时优先尝试 Native AOT。
- 如果 WinUI 3 或依赖导致 Native AOT 不可行，则退回普通 self-contained `win-arm64` 发布。
- 要求管理员权限运行，以尽量稳定地读取进程元数据。
- 只在托盘 UI 需要展示当前数据时枚举进程。
- 使用 `IsWow64Process2` 检测非原生进程。
- 使用公开 Windows 进程诊断 API 读取累计 CPU 时间。
- 点击托盘图标时显示轻量弹窗。
- 悬停托盘图标时显示累计 CPU 时间最高的 Top N 个非原生进程。

Native Guard 不会实现：

- 按时间窗口采样 CPU 使用率。
- 计算实时 CPU 百分比。
- 估算能耗。
- 在第一版显示逐进程能耗或功耗。
- 使用已弃用的能耗诊断 API 做常驻监控。
- 依赖任务管理器的私有内部实现。

## 设计依据

Windows 对进程架构和累计 CPU 时间有稳定公开 API：

- `IsWow64Process2` 可以返回进程机器类型和宿主机器类型，适合判断 x64/x86 进程是否运行在 ARM64 的仿真层上。
- `GetProcessTimes` 或 WinRT 进程诊断 API 可以返回进程累计的内核态和用户态 CPU 时间。

调研后没有发现一个稳定、简单、公开的 API，可以为任意 Win32 进程提供类似任务管理器 “Power usage” 列的逐进程功耗数据。WinRT 的能耗诊断 API 主要面向当前 app 调试，已经标记为 deprecated，并且官方文档说明它本身可能带来明显 CPU 开销。因此 Native Guard 第一版不显示能耗列，除非后续能证明存在合适的低开销系统数据源。

## 用户体验

应用启动后只在 Windows 通知区域放置一个托盘图标，不主动打开主窗口。

点击托盘图标会打开一个紧凑的 WinUI 3 弹窗，里面用表格展示当前非原生进程。每一行包含：

- 进程名
- PID
- 检测到的架构，例如 x64 或 x86
- 累计 CPU 时间

悬停托盘图标时，tooltip 显示累计 CPU 时间最高的 Top N 个非原生进程。默认 N 为 5。

UI 只在用户打开弹窗、悬停托盘图标，或手动刷新时读取一次当前数据。应用没有后台轮询循环。

如果读取过程中某个进程已经退出、拒绝访问，或属于受保护进程，Native Guard 会在本次刷新中跳过该进程。

## 架构

应用拆分为几个小组件：

- `TrayApp`：负责应用启动、单实例行为、托盘图标生命周期、弹窗打开和退出。
- `ProcessInventory`：枚举进程 ID，并用最小必要权限打开进程句柄。
- `ProcessArchitectureDetector`：封装 `IsWow64Process2`，判断进程在 ARM64 宿主上是原生还是非原生。
- `ProcessCpuTimeReader`：通过 `GetProcessTimes` 或等价公开诊断 API 读取累计内核态和用户态 CPU 时间。
- `NonNativeProcessService`：组合进程枚举、架构检测和 CPU 时间读取，生成不可变的展示模型。
- `TrayTooltipFormatter`：按累计 CPU 时间选出 Top N 非原生进程，并格式化 tooltip 文本。
- `ProcessListViewModel`：向 WinUI 弹窗暴露当前进程行数据。

UI 层不直接调用 Win32 API，只向 `NonNativeProcessService` 请求当前进程快照。

## 数据流

当用户请求当前数据时：

1. `TrayApp` 请求 `NonNativeProcessService` 获取新快照。
2. `ProcessInventory` 枚举当前进程 ID。
3. 每个可访问进程交给 `ProcessArchitectureDetector` 检查架构。
4. 原生 ARM64 进程会被过滤掉。
5. `ProcessCpuTimeReader` 为剩余进程读取累计 CPU 时间。
6. 结果行按展示需求排序。
7. 弹窗或 tooltip 渲染这份快照。

应用不保留 CPU 历史数据，也不计算 CPU 时间增量。

## 错误处理

进程枚举采用 best effort 策略。访问被拒绝、进程已退出、受保护进程，以及临时 Win32 错误，都只影响对应进程，不会导致整次刷新失败。

如果应用不是管理员权限运行，应用仍可启动，但当过多进程无法读取时，UI 应提示建议以管理员权限运行。

如果 `IsWow64Process2` 因任何原因不可用，应用会报告架构检测不可用，不做猜测。

## Native AOT 策略

项目目标为 .NET 10 和 `win-arm64`。

发布时先尝试 Native AOT：

- `RuntimeIdentifier=win-arm64`
- `SelfContained=true`
- `PublishAot=true`

如果 WinUI 3、Windows App SDK、XAML 生成、COM 激活或必要 interop 导致 Native AOT 无法生成可工作的应用，则退回不带 `PublishAot` 的 self-contained `win-arm64` 发布。

实现时需要把 interop 逻辑隔离起来，保证 Native AOT 和非 AOT 发布之间不改变应用行为。

## 测试策略

核心逻辑应尽量脱离 WinUI shell 测试：

- 根据 `IsWow64Process2` 的 machine 值测试架构分类。
- 测试非原生进程过滤逻辑。
- 测试 CPU 时间格式化。
- 测试 tooltip Top N 选择和排序。
- 测试单个进程读取失败时不会影响整个刷新。

集成验证包括：

- 构建 solution。
- 运行测试。
- 尝试以 Native AOT 发布 `win-arm64`。
- 如果 Native AOT 因平台原因失败，则发布 self-contained `win-arm64`。
- 在 Windows ARM64 上手动验证托盘图标、点击弹窗、tooltip 内容和刷新行为。

## 实现备注

第一版优先使用最简单稳定的 Win32 interop 路径：

- `OpenProcess`，使用最小查询权限。
- `IsWow64Process2`，用于架构检测。
- `GetProcessTimes`，用于累计 CPU 时间。

如果 WinRT `ProcessDiagnosticInfo` 能减少 interop 复杂度，可以在实现时评估。但它不能强制应用变成 packaged-only 行为，也不能增加常驻运行开销。
