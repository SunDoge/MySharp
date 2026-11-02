# MySharp

[![CI](https://github.com/SunDoge/MySharp/actions/workflows/ci.yml/badge.svg)](https://github.com/SunDoge/MySharp/actions/workflows/ci.yml) · [Apache-2.0](LICENSE)

当前为 GitHub 源码预览，尚未发布 NuGet.org。预留包名为 `SunDoge.MySharp`、`SunDoge.MySharp.Desktop`、`SunDoge.MySharp.HotReload`，代码命名空间统一保留 `MySharp`。

基于 Avalonia 12.1.4 的纯 C# UI 构建库原型。参考 MyGo 的 context/children API，以及 Plico 的普通函数组件、事件触发声明、保留节点与紧凑链式格式。

无 XAML、JavaScript、自定义语言或 source generator 依赖。编译器用 CallerFilePath / CallerLineNumber 自动提供调用位置，框架据此识别节点。声明在创建视图、事件和显式失效后运行，空闲时不循环重建。Avalonia 负责窗口、布局、输入、焦点、主题和绘制。

## 写法

```csharp
using MySharp;

sealed class Counter
{
    private int count;

    [Component]
    public void Build(UiContext cx)
    {
        Ui.Column(cx).Fill().Center().Gap(12).Padding(24).Children(() =>
        {
            Ui.Text(cx, $"Count: {count}").FontSize(28).Bold();
            if (Ui.Button(cx, "Increment").Clicked()) count++;
            Ui.Button(cx, "Reset").Disabled(count == 0).OnClick(() => count = 0);
        });
    }
}

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var counter = new Counter();
        MySharpApp.Run(counter.Build, new WindowOptions { Title = "Counter" }, args);
    }
}
```

`[Component]` 只标记供 formatter 处理的方法，不参与运行时识别，也不是 UI 构建的必要条件。组件就是接受 `UiContext` 的普通函数，不要求 partial、非泛型容器或生成入口。`Children` 闭包同步执行，可直接使用 `if`、`foreach` 和局部变量。

C# 的原生表达式语句不能直接照搬 Plico 的 `layout + closure;`；这里采用原生 `.Children(() => { ... })`，不引入预处理器。

## 工程结构

| 项目 | 职责 |
| --- | --- |
| `src/MySharp` | 核心库：构建 context、链式属性、控件复用；仅依赖 Avalonia |
| `src/MySharp.Desktop` | 可选桌面宿主：窗口、Fluent 主题、Inter 字体 |
| `MySharp.csproj` | 可运行示例，源码位于 `examples/Workspace/Program.cs` |
| `src/MySharp.HotReload` | 可选开发辅助库：代码更新后刷新窗口，Release 示例不依赖 |
| `tools/MySharp.Format` | 独立 Roslyn formatter，不进入 GUI 的运行依赖 |
| `tests/MySharp.Tests` | Headless 行为测试和 formatter 测试，可直接运行 |

其他应用可以直接 ProjectReference `src/MySharp.Desktop/MySharp.Desktop.csproj`。已有 Avalonia 应用只需引用核心库，把 `new UiView(Build)` 放入窗口内容；视图生命周期结束时调用 `Dispose()`。

## 开发时 Hot Reload

Workspace 示例和 OCR 应用的 Debug 构建已接入可选的 `MySharp.HotReload`，用 `dotnet watch --project MySharp.csproj run` 启动。支持的代码更新会重新声明 UI，身份不变的控件和局部状态会保留；移动行号或结构性修改可能重置局部状态，不支持的修改仍需重启。辅助库仅在代码更新时工作，不改核心重建/绘制路径，示例 Release 不依赖它。使用与实测见 [HOT-RELOAD.md](docs/HOT-RELOAD.md)。

## 自动调用位置与实例 Scope

所有控件工厂默认接受编译器隐式传入的 `[CallerFilePath]` 和 `[CallerLineNumber]`，日常调用不需要填写参数：

```csharp
Ui.Column(cx).Gap(12).Children(() =>
{
    Ui.Text(cx, "Hello").FontSize(24);
    Ui.Button(cx, "Save").OnClick(Save);
});
```

节点身份由父节点数值 ID、当前 Scope、调用文件和行号组合决定，不再每帧拼接完整字符串路径。同一行的多个调用按该位置在当前父节点/Scope 内的出现次数区分，计数每次 build 重置。文件和行号的 64 位散列会缓存；无运行时调用栈检查或源码读取。

**普通函数组件的调用作用域：** 框架不能反查任意普通 C# 函数的上层调用位置。同一组件在不同位置复用时，通过无 key Scope 包装，Scope 的调用位置也由编译器自动传入：

```csharp
cx.Scope(() => Profile(cx, leftUser));
cx.Scope(() => Profile(cx, rightUser));
```

**动态实例：** 用业务 ID 提供稳定作用域，列表排序和插入不影响已有实例。业务 Scope 支持 string、long（含 int）和 Guid：

```csharp
foreach (var item in items)
    cx.Scope(item.Id, () => ItemRow(cx, item));
```

同一父级有多组可能使用相同业务 ID 的列表时，各自再包一层 `cx.Scope(() => { ... })`，区别列表调用位置。Scope 不创建控件或布局节点；它同步执行闭包，异常时也恢复上层作用域。

边界与覆盖规则：

- 源码不变化时，调用位置稳定；其他行的条件分支增删不会使后续控件身份移位。
- **同一行上的条件调用若改变，后续同位置的序号会移动。** 需要稳定保留输入状态时，把独立控件写在不同行，或使用显式 key/Scope。无业务 ID 的循环同样只能按局部顺序匹配。
- 不要把动态列表的排序索引当业务 ID；重复的同级业务 Scope 会抛异常。
- 格式化、插入行、移动文件以及 PathMap 变化都可能改变身份。这里不承诺热重载或跨源码版本保留控件状态，也不是持久化主键。
- 高级调用仍可显式传 `site: new CallSiteId(...)`，覆盖默认调用位置。非零 site 为完整显式调用点，相同路径的重复显式 site 会报错；可配合业务 Scope 复用。
- 只传显式 `key:` 时，key 覆盖默认调用位置，允许声明在源码不同分支之间移动而保持同一控件。site 与 key 同时提供则共同构成身份。
- 自定义控件包装函数若要透传调用位置，应在包装函数参数上也加 CallerFilePath / CallerLineNumber，再传给底层工厂，或由调用方使用自动 Scope。
- `[Component]` 和 `[Widget]` 均不注入或改写代码；生成器及其 analyzer 引用已经移除。`CallSiteId` 只是保留的显式覆盖类型。

## 当前能力与约定

- 布局：Column、Row、Grid 的 Rows/Cell/span、Scroll、Space、Dock、Wrap；布局附加属性可用 SetOuter。
- 常用控件：Text、Button、TextInput、Checkbox、Switch、Toggle、Radio、Select、Slider、NumberInput、DateInput、TimeInput、Progress、Image、Separator、List、Tree、Tabs、Menu、Expander。
- 任意 Avalonia 控件可通过 `Ui.Control<T>()` / factory 接入；需要直接父布局的控件（如 GridSplitter）用 `Ui.Direct<T>()`。`Set` 修改 native 控件，`SetOuter` 修改外层布局装饰；`On` / `OnChanged` 管理事件，`Configure` / `Native<T>` 保留原生 API 出口。
- 属性只在实际变化时设置；移除修饰只清理上次声明的值。Immutable brush、frame 和协调集合复用。具体功能与验证状态见 [API-COVERAGE](docs/API-COVERAGE.md)，尚不能宣称完整 Avalonia 功能验证完成。
- `Element` 是 16 字节的 readonly 值句柄，仅当前 build 有效；可用 IsValid 检查，不存进长期状态。关闭/代数回绕会使旧 owner 失效，旧句柄不保留整个视图。
- `Clicked()` 消费一次事件并补一轮更新；`OnClick()` 使用当前事件回调。状态可保留普通字段，也可用 State API 自动跟踪。
- `Rebuild()` 仅 UI 线程同步执行；`Invalidate()` 支持任意线程并合并请求。未声明的节点、Scope、local state、tasks 和 owned resources 会清理。声明异常会传播，尚无事务回滚。

## State 与 async task

```csharp
var count = cx.State(0);                   // 随调用点/Scope 保留，卸载即释放
Ui.Text(cx, count.Value.ToString());
Ui.Button(cx, "Increment").OnClick(() => count.Value++);
var name = cx.State("MySharp");
Ui.TextInput(cx, name);                    // 双向绑定

var request = cx.Task(name.Value, async (string name, CancellationToken token) =>
{
    await Task.Delay(250, token);
    return name.ToUpperInvariant();
});
Ui.Text(cx, request.IsRunning ? "Loading…" : request.Result ?? request.Error?.Message ?? "");
```

共享状态用 `State.Create(initial)`。Value 在 build 中读取会订阅该视图，相等赋值不更新；动态实例放进业务 Scope。集合原地修改用 `Mutate`，后台写状态用 `SetAsync/UpdateAsync`。`cx.Task<T>()` 是手动任务，支持 Run/RunOnUi、Cancel、Restart；带依赖 key 的 Task 自动替换运行并丢弃过期结果。工作异常成为 Error 状态；卸载会取消。原生 picker/clipboard 等 UI-affine async 操作用 RunOnUi。`cx.Resource(factory)` 管理 IDisposable 的作用域生命周期。

详见 [STATE-AND-TASKS](docs/STATE-AND-TASKS.md)。[PROFILING](docs/PROFILING.md) 记录前后 trace/基准，参考 MyGo 的进一步优化见 [MYGO-OPTIMIZATIONS](docs/MYGO-OPTIMIZATIONS.md)。

## 窗口与标题栏

WindowOptions 支持 System/Custom/Borderless chrome、大小/位置/约束、可缩放、任务栏、置顶、状态、图标与生命周期回调。`ConfigureWindow`、`configureApp/configureApplication/configureLifetime` 保留原生设置、菜单和托盘出口。

`cx.Window` 提供窗口命令、状态订阅、owned/modal helpers，`MySharpApp.CreateWindow` 创建额外窗口。布局元素用 `.TitleBar()` 或 `.DecorationRole()` 标记原生区域，`Ui.WindowControls(cx)` 提供应用绘制按钮。`cx.TopLevel/Clipboard/Storage/Screens/Launcher/PlatformHandle` 在挂载后提供原生服务。

```bash
dotnet run -- --page=lab --custom-chrome
```

当前只有 Headless 和 X11/Xvfb 截图/启动证据；原生 WM 拖动/缩放、Windows/macOS caption 行为还需要验证，具体列在能力矩阵。

## 运行与验证

需要 .NET SDK 10.0.403（由 `global.json` 固定）和当前平台支持的 Avalonia 桌面环境。安装 SDK 后可运行：

```bash
dotnet run

dotnet run --project tests/MySharp.Tests
bash scripts/screenshot.sh
```

矢量图标使用 Lucide，默认 16px、图文间距 8px，并跟随原生控件颜色：

```csharp
Ui.Icon(cx, Icons.Settings);
Ui.Button(cx, "Settings").Icon(Icons.Settings);
Ui.IconButton(cx, Icons.Trash, "Delete task");
```

单选分段控件可用 `Ui.Segmented(cx, items, selectedIndex)`，其中 `selectedIndex` 为 `State<int>`；也可传入索引和回调。`Weight(FontWeight.Medium)` 可以表达较轻的小标题层级。

纯图标按钮要求提供可访问名称，并自动用作 tooltip。完整图标枚举为 `Lucide.Avalonia.LucideIconKind`。[主题与图标说明](docs/THEME.md)。

示例现在有五个页面，可在侧栏切换，状态由应用持有：

- **Overview**（`examples/Workspace/Overview.cs`）：项目统计、搜索、空状态和活动记录。
- **Tasks**（`examples/Workspace/Tasks.cs`）：新增任务、完成/重新打开、状态筛选、清除已完成；任务行按业务 ID Scope 复用。
- **Settings**（`examples/Workspace/Settings.cs`）：草稿修改后保存，更新侧栏资料、活动区域显示和任务行密度；仅保存在本次会话。
- **Components**（`examples/Workspace/Controls.cs`）：默认按钮、输入框、下拉框、复选框、单选框、开关、滑块和进度条；支持 `--page=controls --dark`。
- **State & tasks**（`examples/Workspace/Lab.cs`）：局部/共享 state、原生双向绑定、任务取消/错误与依赖自动预览。

可以用 `dotnet run -- --page=tasks` 或 `dotnet run -- --page=settings` 直接打开指定页。

`bash scripts/screenshot.sh` 会拍摄全部五个页面和自定义标题栏，截图位于 `artifacts/overview.png`、`artifacts/tasks.png`、`artifacts/settings.png`、`artifacts/lab.png`、`artifacts/controls.png`、`artifacts/controls-dark.png`、`artifacts/custom-chrome.png`；`artifacts/gui.png` 为概览副本。图形桌面用 `dotnet run`，无桌面时脚本使用 Xvfb。

当前测试覆盖自动身份/数值 key 域、16 字节句柄/代数回绕/GC、保留节点/焦点/属性通知、state/task race 与生命周期、原生控件/事件、窗口/模态/closing、布局附加属性和示例页面。当前框架行为测试为 82 项。运行脚本生成的完整日志位于本地 `artifacts/`，不提交构建产物；可运行 `scripts/` 中的脚本生成测量结果，能力矩阵列出尚未验证部分。

## 紧凑 formatter

C# 可以使用 Roslyn 解析语法树再实现自己的排版规则。本工具只处理标有 `[Component]`（兼容旧 `[UiComponent]`）的方法内部调用，其他代码原样保留。

```bash
dotnet run --project tools/MySharp.Format -- examples/Workspace/Program.cs
dotnet run --project tools/MySharp.Format -- --check --width 120 examples/Workspace/Program.cs
```

例如：

```csharp
// Before
Ui.Text(cx, "Title")
    .FontSize(28)
    .Bold();

// After
Ui.Text(cx, "Title").FontSize(28).Bold();
```

- 尽量展平属性链；超出宽度时在链式调用边界换行。单个字符串或调用不会被强行拆开，因此宽度是软上限。
- Children 的 lambda、控制流和普通代码块不整体压成一行；其中的属性链可独立展平。
- 含注释、指令或多行字面量的表达式保守跳过，不使用正则替换源码。
- 写回前重解析并比较所有 token 的种类和原文；无效源码拒绝处理。支持多文件先验证再写回、单文件临时写入替换、拒绝符号链接。不是多文件事务。
- `--check` 不写文件，需格式化返回 1，参数/语法错误返回 2。
- 这是 UI 链式 formatter 原型，不是完整 C# formatter 或 IDE 扩展；标记按语法名称匹配，不做符号解析。先用 IDE 或 `dotnet format` 处理常规代码，再运行本工具。SDK 内的 Roslyn 用于开发工具，核心库没有 Roslyn 依赖。

## XML 工程文件格式化

仓库本地工具固定 CSharpier 1.3.0，配置位于 `.config/dotnet-tools.json`。仅格式化 XML 工程/配置文件；`.csharpierignore` 排除 C# 源码，保留上面的紧凑 formatter 写法，也排除构建产物。

```bash
dotnet tool restore
dotnet csharpier format .
dotnet csharpier check .
```

CI 执行只读 `check`；普通构建不会自动运行 formatter。

## 编译测速

```bash
dotnet restore
python3 scripts/benchmark.py
```

早期自动调用位置版本实测（7 轮中位数，不含依赖恢复；新增 API 与优化之前）：

| 场景 | 耗时 |
| --- | --- |
| 清理后构建 | 1.193 秒 |
| 无改动构建 | 0.755 秒 |
| 修改页面后增量构建 | 0.804 秒 |

移除生成器前的三页演示实测（本机，7 轮中位数；历史数据）：

| 场景 | 耗时 |
| --- | --- |
| 清理后构建 | 1.212 秒 |
| 无改动构建 | 0.736 秒 |
| 修改页面后增量构建 | 0.803 秒 |

启用增量生成器时的单页版本实测（早于当前三页演示）（含核心库、桌面宿主、生成器和示例项目）：

| 场景 | 7 轮中位数 |
| --- | --- |
| 清理后构建 | 1.172 秒 |
| 无改动构建 | 0.741 秒 |
| 修改示例后增量构建 | 0.779 秒 |

引入生成器之前的拆库版本（核心库 + 桌面宿主 + 示例）实测：

| 场景 | 7 轮中位数 |
| --- | --- |
| 清理后构建 | 0.988 秒 |
| 无改动构建 | 0.706 秒 |
| 修改示例后增量构建 | 0.749 秒 |

每类 7 轮取中位数，最新结果在 `artifacts/build-times.json`；引入生成器前拆库版本在 `artifacts/build-times-builder.json`，拆库前静态版结果在 `artifacts/build-times-static.json`。

- `clean`：每轮先 `dotnet clean`，只计随后 `dotnet build --no-restore -c Debug`。包含项目引用构建，保留 NuGet、操作系统文件缓存和编译器服务，不重编 Avalonia 本身。
- `no_change`：不改源码，执行同一构建命令。
- `edit`：每轮修改示例实际显示的一处字符串，构建后自动恢复源码。
- `first_build_seconds`：本轮脚本开始时的构建，不代表冷启动。

环境：Debian 13、Linux x64、Intel Core Ultra 9 285K（24 核）、SDK 10.0.403。使用默认 Debug/共享编译器；不含下载、启动、发布或 Native AOT。历史静态单项目首次构建约 1.654 秒、依赖恢复约 1 分 25 秒。小项目成绩不能代表大型应用；未进行等价 Go 对照测试。


## 运行效率测量

```bash
dotnet run --project benchmarks/MySharp.Benchmarks -c Release
xvfb-run -a -s '-screen 0 1280x900x24' python3 scripts/runtime-idle.py
```

请顺序执行，避免并发基准相互争抢 CPU。第二条使用第一条已经构建的 Release 桌面程序。

- `artifacts/runtime-rebuild.json`：Avalonia.Headless + Fluent + Inter，窗口 1120×820；至少 500 次且不少于 2 秒预热，每项测 7 批、每批 200 次。每个样本是批平均耗时，报告其中位数及最大批平均值，不把它当作单次延迟 P95。
- 重建包括 UI 声明、控件协调、消息队列和同步布局；项目筛选项还交替修改查询触发节点增删。排除原生呈现、GPU 绘制、真实输入延迟，不能换算为完整应用帧率。
- 分配量使用 `GC.GetAllocatedBytesForCurrentThread()`，表示每次操作在 UI 线程产生的托管对象，不是常驻内存，也不是整个进程的全部分配。
- `artifacts/runtime-idle.json`：真实 Release Avalonia 桌面窗口在 Linux X11/Xvfb 中运行，每页等待 5 秒，再采样 8 秒空闲 CPU 和 `/proc/.../smaps_rollup`。100% CPU 表示占满一个核心；不包含 Xvfb 或进程外的 GPU/驱动内存。每页只有一次空闲采样，不是长期泄漏或动画压力测试。

以下运行数据来自切换自动调用位置之前的三页版本（同一台 Core Ultra 9 285K / Debian 13），未针对新的身份机制重新采样：

| 页面/操作 | 声明与布局耗时 | UI 线程分配/次 |
| --- | --- | --- |
| 概览无变化重建 | 0.364 ms | 211 KiB |
| 任务无变化重建 | 0.359 ms | 213 KiB |
| 设置无变化重建 | 0.268 ms | 147 KiB |
| 项目筛选变化 | 0.559 ms | 390 KiB |

真实窗口空闲采样：RSS 182–188 MiB，PSS 147–154 MiB，CPU 为单核心的 0.25–0.50%。这是 Xvfb 环境的短时观测；不能据此推断硬件 GPU 表现或长期内存走势。

当前这几个页面的重建开销较低，但整体内存不算轻量。完整视图遍历、属性重置、路径字符串和临时集合仍有优化空间；有 retained 节点不等于零分配。没有进行大列表、动画帧率或等价 MyGo 对照测试。


## 发布体积

当前自动调用位置三页版本，Release / linux-x64、关闭调试符号，按发布文件实际字节数统计：

| 部署方式 | 目录体积 | 需要另装 .NET |
| --- | --- | --- |
| Framework-dependent | 24.013 MiB | 是 |
| Self-contained（未裁剪） | 102.761 MiB | 否 |
| Native AOT（默认裁剪、剥离符号） | 33.568 MiB | 否 |

Native AOT 的 3 个分发文件为：程序约 20.24 MiB、`libSkiaSharp.so` 约 10.65 MiB、`libHarfBuzzSharp.so` 约 2.68 MiB。普通发布中，应用 + MySharp + Desktop 自身的三个 DLL 合计约 61.5 KiB；完整分发体积应看上表，不能只看应用 DLL。

这不是单文件打包，也不包含压缩；仍需要目标系统的图形等原生依赖。调试符号不计入分发，上述三种发布本次都未生成符号侧车。日常 bin 目录可能包含其他 RID 与历史构建文件，不是分发体积。

可复现命令（从仓库根目录运行，首次发布还可能下载运行时或 AOT 工具包）：

```bash
dotnet publish MySharp.csproj -c Release -r linux-x64 --self-contained false -p:DebugType=None -p:DebugSymbols=false -o artifacts/publish/framework-dependent
dotnet publish MySharp.csproj -c Release -r linux-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o artifacts/publish/self-contained
dotnet publish MySharp.csproj -c Release -r linux-x64 --self-contained true -p:PublishAot=true -p:StripSymbols=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/publish/native-aot
```

运行 `xvfb-run -a -s '-screen 0 1280x900x24 -noreset' python3 scripts/publish-smoke.py` 可复现发布程序启动检查。`-noreset` 避免多次启动之间 Xvfb 显示重置的竞争。

原始统计在 `artifacts/publish-sizes.json`；三种部署方式均在 Xvfb 中验证了三个页面可以创建窗口，记录在 `artifacts/publish-smoke.json`。AOT 的该项验证是启动冒烟检查，不是所有控件行为的完整回归。


## 当前优化检查点

最新未加 trace 的结果在 `artifacts/profile-current-combined.json`：常规页面无变化声明/布局约 0.010–0.014 ms，每次约 2.6–4.0 KiB；独立的 1,000 预生成标签 + static 回调场景约 0.167 ms / 0 B。它们都不是完整 GPU 帧耗时，不能直接与 MyGo 包含 CPU 绘制的 Tester 排名。完整测量条件和历史对照见 docs/PROFILING.md。

MyGo-inspired 16 字节句柄、数值 ID、同级 key 类型域和稀疏存储回收说明在 docs/MYGO-OPTIMIZATIONS.md。测试还覆盖 generation 回绕、旧句柄的 GC、5,000→4 行的保留容量回收与失败首帧的清理。

目标仍在进行，剩余能力/验证项明确列于 docs/API-COVERAGE.md，不将当前原生 API 出口当作完整功能验收。

Input routing, hotkeys and owned flyouts are documented in [INPUT-AND-POPUPS.md](docs/INPUT-AND-POPUPS.md). The lab includes an owned popup and Ctrl+L task shortcut.

Native service access and the Linux clipboard/storage/file-picker verification are described in [NATIVE-SERVICES.md](docs/NATIVE-SERVICES.md).

The default desktop appearance now uses a MyGo-inspired code-only palette and control metrics. See [THEME.md](docs/THEME.md); run the sample with `--dark` for its dark variant.

## 许可证

[Apache License 2.0](LICENSE)，依赖及设计参考见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## 提交规范

使用 Conventional Commits，开发工具版本由 `mise.toml` 管理：

```sh
mise install
cog install-hook commit-msg
cog commit feat "add a component"
cog check
```

提交类型包括 `feat`、`fix`、`perf`、`refactor`、`test`、`docs`、`build`、`ci` 和 `chore`。
克隆仓库后安装 commit-msg hook，即可校验普通 `git commit` 的消息。
图片、测量快照和构建产物仅保留在本地，可用 `scripts/` 重新生成。

### Shader prototypes

独立的 [Shader lab](examples/ShaderLab/README.md) 验证了 shader 背景、控件子树的灰度/波纹效果，以及效果下的原生按钮和输入交互。它不改变框架包的 API 或默认渲染路径；运行方式、原生像素验证和限制见示例说明。
