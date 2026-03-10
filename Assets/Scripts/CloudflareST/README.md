# CloudflareST Unity GUI 集成指南

## 快速开始

### 1. 确保 DLL 已构建并放置

```powershell
# 在 CloudflareSeedTest-CSharp 目录下运行
.\build.ps1 -unity
```

这将构建 `netstandard2.0` 版本的 `CloudflareST.Core.dll` 并自动复制到：
- `Assets/Plugins/CloudflareST/CloudflareST.Core.dll`

### 2. Unity 项目结构

```
Assets/
├── Plugins/
│   └── CloudflareST/
│       └── CloudflareST.Core.dll         # Core 库（netstandard2.0）
├── Scripts/
│   └── CloudflareST/
│       ├── CloudflareST.Unity.asmdef     # Assembly 定义
│       ├── CfstWindowController.cs       # 主窗口控制器（挂 GameObject）
│       ├── CfstConfigPanelController.cs  # 配置面板
│       ├── CfstRunPanelController.cs     # 测速运行面板
│       ├── CfstResultPanelController.cs  # 结果面板
│       ├── CfstSchedulePanelController.cs# 调度面板
│       ├── CfstHistoryPanelController.cs # 历史面板
│       ├── CfstAboutPanelController.cs   # 关于面板
│       └── CfstTestRecord.cs             # 测速记录数据模型
└── UI/
    └── CloudflareST/
        ├── CfstTheme.uss                 # 主题样式
        ├── CfstMainWindow.uxml           # 主窗口（导航 + 内容区）
        ├── CfstConfigPanel.uxml          # 配置面板
        ├── CfstRunPanel.uxml             # 测速运行面板
        ├── CfstResultPanel.uxml          # 结果面板
        ├── CfstSchedulePanel.uxml        # 调度面板
        ├── CfstHistoryPanel.uxml         # 历史面板
        └── CfstAboutPanel.uxml           # 关于面板
```

### 3. 场景设置

1. 创建空的 `GameObject`，命名为 `CloudflareSTWindow`
2. 添加 `UIDocument` 组件，将 `CfstMainWindow.uxml` 拖入 **Source Asset**
3. 添加 `CfstWindowController` 组件
4. 在 Inspector 中填入以下 Panel Assets：
   - **Config Panel Asset** → `CfstConfigPanel.uxml`
   - **Run Panel Asset**    → `CfstRunPanel.uxml`
   - **Result Panel Asset** → `CfstResultPanel.uxml`
   - **Schedule Panel Asset** → `CfstSchedulePanel.uxml`
   - **History Panel Asset** → `CfstHistoryPanel.uxml`
   - **About Panel Asset**  → `CfstAboutPanel.uxml`

### 4. 在脚本中调用 Core API

```csharp
using CloudflareST.Core;
using System.Threading;
using System.Threading.Tasks;

// 创建服务实例
ICoreService core = new CoreService();

// 构建配置
var config = new TestConfig
{
    Concurrency = 200,
    UseTcping   = true,
    OutputFile  = "result.csv",
};

// 运行测速
using var cts = new CancellationTokenSource();
TestResult result = await core.RunTestAsync(config, cts.Token);
Debug.Log($"Success={result.Success} | {result.Summary}");
```

## UI 面板说明

| 面板 | UXML | 控制器 | 功能 |
|---|---|---|---|
| 配置 | CfstConfigPanel.uxml | CfstConfigPanelController | 设置测速参数，自动持久化到 PlayerPrefs |
| 测速 | CfstRunPanel.uxml | CfstRunPanelController | 启动/取消测速，显示进度日志 |
| 结果 | CfstResultPanel.uxml | CfstResultPanelController | 展示测速结果，支持复制/导出 |
| 调度 | CfstSchedulePanel.uxml | CfstSchedulePanelController | 配置定时调度规则 |
| 历史 | CfstHistoryPanel.uxml | CfstHistoryPanelController | 历史记录列表，点击查看详情 |
| 关于 | CfstAboutPanel.uxml | CfstAboutPanelController | 项目信息 |

## 兼容性说明

- Unity 2022.x（UIToolkit 内置）
- Scripting Backend：Mono（.NET Framework 4.x）或 IL2CPP
- `CloudflareST.Core.dll` 目标框架：`netstandard2.0`
- 不依赖第三方 NuGet 包（`System.Text.Json 4.7.2` 已内联）
