# Unity UIToolkit GUI 布局排版图

> 基于 UXML + USS 源码逐元素分析  
> 项目：AIGate · CloudflareSeedTest-CSharp (CloudflareST)  
> 生成日期：2026-03-11

---

## 目录

- [一、AIGate 项目](#一aigate-项目)
  - [1.1 主窗口 GateMainWindow](#11-主窗口-gatemainwindow)
  - [1.2 全局代理 GlobalPanel](#12-全局代理-globalpanel)
  - [1.3 应用代理 AppPanel](#13-应用代理-apppanel)
  - [1.4 预设配置集 PresetPanel](#14-预设配置集-presetpanel)
  - [1.5 状态总览 StatusPanel](#15-状态总览-statuspanel)
  - [1.6 连通性测试 TestPanel](#16-连通性测试-testpanel)
  - [1.7 工具路径 ToolPathPanel](#17-工具路径-toolpathpanel)
- [二、CloudflareST 项目](#二cloudflareseedtest-csharp-项目)
  - [2.1 主窗口 CfstMainWindow](#21-主窗口-cfstmainwindow)
  - [2.2 测速配置 CfstConfigPanel](#22-测速配置-cfstconfigpanel)
  - [2.3 测速进度 CfstRunPanel](#23-测速进度-cfstrunpanel)
  - [2.4 测速结果 CfstResultPanel](#24-测速结果-cfstresultpanel)
  - [2.5 历史记录 CfstHistoryPanel](#25-历史记录-cfsthistorypanel)
  - [2.6 定时调度 CfstSchedulePanel](#26-定时调度-cfstschedulepanel)
  - [2.7 关于 CfstAboutPanel](#27-关于-cfstaboutpanel)
- [三、界面布局核对方案](#三界面布局核对方案)

---

## 一、AIGate 项目

**主题**：深蓝科技风 · 字体：`SourceHanSansSC-Bold SDF`

| 变量 | 值 | 用途 |
|------|-----|------|
| bg | `#0f1117` | 根背景 |
| surface | `#111827` | 卡片、标题栏 |
| sidebar-bg | `#0d1117` | 侧边栏 |
| border | `#1e293b` | 边框 |
| accent | `#4f8ef7` | 选中、主按钮 |
| text-value | `#f59e0b` | 已设置代理值（黄） |
| text-empty | `#334155` | 未设置占位（灰斜） |
| danger | `#f87171` | 危险操作 |
| wizard | `#8b5cf6` | 向导/工具路径（紫） |
| green | `#22c55e` | 已配置状态点 |

---

### 1.1 主窗口 GateMainWindow

**文件**：`Assets/UI/AIGate/GateMainWindow.uxml`

```
gate-root (flex-column 100%x100% bg:#0f1117)
├── gate-titlebar  h=44px bg:#111827 border-bottom:1px #1e293b
│   ├── Label "GATE"          18px bold color:#4f8ef7
│   ├── Label "代理配置管理"   12px color:#475569
│   ├── spacer flex-grow:1
│   └── Label "v1.0"          11px color:#334155
└── gate-body (flex-row flex-grow:1)
    ├── nav-sidebar  w=180px bg:#0d1117 border-right:1px #1e293b
    │   ├── Button nav-global  "[G]  全局代理"  .nav-item--active（默认）
    │   ├── Button nav-app     "[A]  应用代理"  .nav-item
    │   ├── Button nav-preset  "[P]  预  设"    .nav-item
    │   ├── Button nav-status  "[S]  状态总览"  .nav-item
    │   ├── Button nav-test    "[T]  连通测试"  .nav-item
    │   ├── divider  h=1px bg:#1e293b margin:10px 16px
    │   ├── Button nav-wizard  "[W]  配置向导"  .nav-item--wizard（紫）
    │   ├── divider
    │   └── Button nav-paths   "[R]  工具路径"  .nav-item--wizard（紫）
    └── content-area  flex-grow:1 bg:#0f1117 padding:24px
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐ h=44px bg:#111827
│  GATE   代理配置管理                                  v1.0  │
├────────────┬─────────────────────────────────────────────────┤
│ [G]全局代理 │← 选中: bg:#1e2d4f color:#4f8ef7 border-left:3px│
│ [A]应用代理 │  普通: color:#64748b h=42px padding:0 20px     │
│ [P]预  设  │  hover: bg:#1e2436 color:#94a3b8               │
│ [S]状态总览 │                                                 │
│ [T]连通测试 │       content-area  bg:#0f1117  padding:24px   │
│ ─────────  │       C# 动态加载各面板 UXML                    │
│ [W]配置向导 │紫                                               │
│ ─────────  │                                                 │
│ [R]工具路径 │紫                                               │
│  w=180px   │  flex-grow:1                                   │
└────────────┴─────────────────────────────────────────────────┘
```

**导航按钮交互**

| 按钮 | name | 加载面板 | 内容区变化 |
|-----|------|---------|----------|
| [G] 全局代理 | `nav-global` | `GlobalPanel.uxml` | 状态卡 + 设置表单 + 操作行 |
| [A] 应用代理 | `nav-app` | `AppPanel.uxml` | 工具栏 + ListView + 批量栏 |
| [P] 预  设 | `nav-preset` | `PresetPanel.uxml` | 左列预设列表 + 右列详情 |
| [S] 状态总览 | `nav-status` | `StatusPanel.uxml` | 全局卡 + 应用卡 + 预设卡 |
| [T] 连通测试 | `nav-test` | `TestPanel.uxml` | 表单卡 + 大按钮 + 结果卡 |
| [W] 配置向导 | `nav-wizard` | 向导页 | 多步骤引导 |
| [R] 工具路径 | `nav-paths` | `ToolPathPanel.uxml` | 说明卡 + 搜索 + 路径列表 |

---

### 1.2 全局代理 GlobalPanel

**文件**：`Assets/UI/AIGate/GlobalPanel.uxml`

```
panel-root (flex-column)
├── panel-header (flex-row mb:20px pb:14px border-bottom:1px #1e293b)
│   ├── Label "全局代理"           20px bold color:#f1f5f9
│   └── Label "HTTP_PROXY / ..."   12px color:#475569
├── status-card (bg:#111827 r:10px border:1px #1e293b p:16px 20px mb:14px)
│   ├── Label "当前配置"           11px bold #475569
│   ├── status-row → Label"HTTP_PROXY"(w=130px)  + Label(global-http-status)
│   ├── status-row → Label"HTTPS_PROXY"           + Label(global-https-status)
│   └── status-row → Label"NO_PROXY"              + Label(global-noproxy-status)
│       各行: border-bottom:1px #1a2035  padding:8px 0
├── form-card (bg:#111827 r:10px border:1px #1e293b p:16px 20px mb:14px)
│   ├── Label "设置代理"
│   ├── form-row: Label"代理地址"(w=100px bold #94a3b8) + TextField(proxy-input) + Label提示
│   ├── form-row: Label"HTTP"     + TextField(http-input)
│   ├── form-row: Label"HTTPS"    + TextField(https-input)
│   ├── form-row: Label"NO_PROXY" + TextField(noproxy-input)
│   └── form-row: Toggle(verify-toggle  label="设置前测试连通性")
│       TextField样式: bg:#0a0e18 border:1px #1e293b r:6px h=34px
│       focus: border-color:#4f8ef7
├── action-row (flex-row justify:flex-end mt:18px)
│   ├── Button(btn-clear)    "清除代理"  .btn-danger   透明bg 红字红边框
│   ├── Button(btn-refresh)  "刷新状态"  .btn-secondary bg:#1e293b 灰字
│   └── Button(btn-apply)    "应用设置"  .btn-primary  bg:#4f8ef7 白字  h=36px
└── Label(global-feedback)   text:""  min-h:18px  text-align:center
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐
│ 全局代理                                                     │
│ HTTP_PROXY / HTTPS_PROXY / NO_PROXY                         │
├──────────────────────────────────────────────────────────────┤
│ ┌─ 当前配置 bg:#111827 r:10px ────────────────────────────┐  │
│ │ HTTP_PROXY   │ (未设置)  ← 灰斜 #334155               │  │
│ │ ───────────────────────────────────────────────────    │  │
│ │ HTTPS_PROXY  │ (未设置)                               │  │
│ │ ───────────────────────────────────────────────────    │  │
│ │ NO_PROXY     │ (未设置)                               │  │
│ └─────────────────────────────────────────────────────── ┘  │
│ ┌─ 设置代理 bg:#111827 r:10px ────────────────────────────┐  │
│ │ 代理地址 [http://host:port_________]  同时设置H/HTTPS   │  │
│ │ HTTP     [http://host:port_________]                   │  │
│ │ HTTPS    [https://host:port________]                   │  │
│ │ NO_PROXY [localhost,127.0.0.1______]                   │  │
│ │ □ 设置前测试连通性                                      │  │
│ └─────────────────────────────────────────────────────── ┘  │
│                   [清除代理]  [刷新状态]  [应用设置]          │
│  ● 反馈文字（居中 min-h:18px）                               │
└──────────────────────────────────────────────────────────────┘
```

**按钮交互**

| 按钮 | name | 点击后界面变化 |
|-----|------|---------------|
| 清除代理 | `btn-clear` | 三行→灰斜体 `(未设置)`；反馈蓝色 |
| 刷新状态 | `btn-refresh` | 重读环境变量；有值→黄 `#f59e0b`，无→灰斜 |
| 应用设置 | `btn-apply` | 状态卡对应行变黄色显示新值；反馈绿色 |

verify-toggle 勾选时：应用前先测连通，失败反馈红色，不写入。

---

### 1.3 应用代理 AppPanel

**文件**：`Assets/UI/AIGate/AppPanel.uxml`

```
panel-root (flex-column)
├── panel-header
│   ├── Label "应用代理配置"        20px bold
│   └── Label "支持 130+ 工具..."   12px #475569
├── toolbar (flex-row mb:10px)
│   ├── TextField(app-search)       flex-grow:1  placeholder"搜索应用..."
│   ├── DropdownField(category-filter)  w=130px  mr:10px
│   └── Toggle(installed-only-toggle)   label="仅显示已安装"
├── list-header (flex-row bg:#0d1520 r:6px6px00 border:1px #1e293b)
│   ├── Label "应用名称"  w=180px  11px bold #475569
│   ├── Label "分类"      flex-grow:1
│   ├── Label "状态"      w=80px   居中
│   └── Label "操作"      w=130px  右对齐
├── ListView(app-list)  item-height=44px  flex-grow:1  bg:#0a0e18 r:8px
│   └── 每行 .list-row h=44px (C# makeItem/bindItem):
│       ├── Label 应用名称  w=180px  13px bold #e2e8f0
│       ├── Label 分类      flex-grow:1  12px #475569
│       ├── VisualElement 状态点  8x8px r:4px
│       │   已配置: bg:#22c55e   未配置: bg:#334155
│       ├── Button "设置"  h=30px 12px
│       └── Button "清除"  h=30px 12px
├── batch-bar (flex-row bg:#111827 r:8px border:1px #1e293b p:8px 14px mb:10px)
│   ├── Label(batch-count)           "已选 0 个"  w=64px color:#4f8ef7
│   ├── TextField(batch-proxy-input) flex-grow:1  mr:10px
│   ├── Button(btn-select-installed) "全选已安装"  .btn-secondary .btn-sm
│   ├── Button(btn-batch-set)        "批量设置"    .btn-primary   .btn-sm
│   └── Button(btn-batch-clear)      "批量清除"    .btn-danger    .btn-sm
├── Label(app-feedback)
└── edit-overlay (position:absolute 全覆盖 rgba(0,0,0,0.75) display:none)
    └── overlay-panel w=460px bg:#111827 r:14px p:28px 32px
        ├── Label(edit-app-name)  "编辑: {appname}"  18px bold
        ├── Label  "输入完整代理地址，如 http://host:port"
        ├── form-row: Label"代理地址" + TextField(edit-proxy-input)
        └── overlay-actions (flex-row justify:flex-end mt:20px)
            ├── Button(btn-edit-cancel)  "取消"  .btn-secondary
            └── Button(btn-edit-save)    "保存"  .btn-primary
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐
│ 应用代理配置                                                  │
│ 支持 130+ 工具，逗号分隔批量操作                               │
├──────────────────────────────────────────────────────────────┤
│ [搜索应用____________]  [全部分类 ▾]  □ 仅显示已安装          │
├─────────────────┬──────────┬────────┬────────────────────────┤
│ 应用名称(w=180) │ 分类     │ 状态   │ 操作(右对齐 w=130)     │
├──────────────────────────────────────────────────────────────┤
│ Git          │ 版本控制 │ ●绿    │       [设置]  [清除]   │
│ npm          │ 包管理器 │ ○灰    │       [设置]  [清除]   │
│ pip          │ 包管理器 │ ●绿    │       [设置]  [清除]   │
│ VSCode       │ AI IDE   │ ○灰    │       [设置]  [清除]   │
│ ...          │ ...      │ ...    │       ...              │
│              ListView item-height=44px bg:#0a0e18          │
├──────────────────────────────────────────────────────────────┤
│ 已选0个 [批量代理地址______]  [全选已安装] [批量设置] [批量清除]│
│  ● 反馈文字                                                  │
└──────────────────────────────────────────────────────────────┘

  ╔═ 编辑弹窗（display:none，点[设置]后→flex）════════════════╗
  ║  遮罩 position:absolute  rgba(0,0,0,0.75)                ║
  ║  ┌── overlay-panel w=460px bg:#111827 r:14px ──────────┐ ║
  ║  │  编辑: {appname}              18px bold             │ ║
  ║  │  输入完整代理地址，如 http://host:port              │ ║
  ║  │  代理地址 [http://host:port___________________]     │ ║
  ║  │                           [取消]  [保存]           │ ║
  ║  └───────────────────────────────────────────────────  ┘ ║
  ╚═══════════════════════════════════════════════════════════╝
```

**按钮交互**

| 按钮 | name | 点击后界面变化 |
|-----|------|---------------|
| 列表行 [设置] | — | edit-overlay 显示；标题→"编辑: {appname}" |
| 列表行 [清除] | — | 该行状态点→灰；反馈提示已清除 |
| 全选已安装 | `btn-select-installed` | 所有已安装行选中；batch-count 数字更新 |
| 批量设置 | `btn-batch-set` | 选中行写入批量代理值；状态点→绿 |
| 批量清除 | `btn-batch-clear` | 选中行代理清除；状态点→灰 |
| 编辑弹窗 取消 | `btn-edit-cancel` | overlay display→none，不保存 |
| 编辑弹窗 保存 | `btn-edit-save` | 写入配置；关闭弹窗；刷新状态点 |

搜索栏实时过滤 ListView（不区分大小写）；分类下拉同步过滤；installed-only-toggle 仅显示已检测到的已安装工具。

---

### 1.4 预设配置集 PresetPanel

**文件**：`Assets/UI/AIGate/PresetPanel.uxml`

```
panel-root (flex-column)
├── panel-header
│   ├── Label "💾 预设配置集"        20px bold
│   └── Label "Preset Management..." 12px #475569
├── preset-body (flex-row flex-grow:1 mb:14px)
│   ├── preset-list-pane (w=210px flex-column mr:14px)
│   │   ├── Label "已保存的预设"     11px bold #475569
│   │   ├── ListView(preset-list)   item-height=40px  flex-grow:1
│   │   │   bg:#0a0e18 r:8px border:1px #1e293b
│   │   └── Button(btn-new-preset)  "+ 新建预设"  .btn-primary .btn-full
│   └── preset-detail-pane (flex-grow:1 bg:#111827 r:10px border:1px #1e293b p:16px 20px)
│       ├── Label "预设详情"         11px bold #475569
│       ├── detail-row → Label"名称"     + Label(detail-name)
│       ├── detail-row → Label"创建时间" + Label(detail-created)
│       ├── detail-row → Label"更新时间" + Label(detail-updated)
│       ├── detail-row → Label"HTTP_PROXY" + Label(detail-http)  color:#f59e0b
│       ├── detail-row → Label"应用配置数" + Label(detail-app-count)
│       └── detail-actions (flex-row flex-wrap mt:14px)
│           ├── Button(btn-apply-preset)  "应用"     .btn-primary  .btn-sm
│           ├── Button(btn-set-default)   "设为默认"  .btn-secondary .btn-sm
│           ├── Button(btn-export-preset) "导出"     .btn-secondary .btn-sm
│           └── Button(btn-delete-preset) "删除"     .btn-danger   .btn-sm
├── save-bar (flex-row bg:#111827 r:8px border:1px #1e293b p:10px 16px mb:10px)
│   ├── Label "保存当前配置："       12px #94a3b8
│   ├── TextField(save-name-input)  flex-grow:1  mr:10px
│   └── Button(btn-save-preset)     "保存"  .btn-primary
├── Label(preset-feedback)
└── new-preset-overlay (position:absolute 全覆盖 rgba(0,0,0,0.75) display:none)
    └── overlay-panel w=460px bg:#111827 r:14px p:28px 32px
        ├── Label "新建预设"          18px bold
        ├── Label "输入名称后将保存当前配置"
        ├── form-row: Label"预设名称" + TextField(new-preset-name)
        ├── form-row: Label"描述（可选）" + TextField(new-preset-desc)
        └── overlay-actions
            ├── Button(btn-new-cancel) "取消"  .btn-secondary
            └── Button(btn-new-save)   "创建"  .btn-primary
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐
│ 💾 预设配置集                                                │
│ Preset Management · 保存/加载代理场景                         │
├───────────────────┬──────────────────────────────────────────┤
│ ┌─ 已保存的预设 ─┐ │ ┌─ 预设详情 bg:#111827 ──────────────┐  │
│ │ 默认预设       │ │ │ 名称      │ 默认预设               │  │
│ │ 办公室         │ │ │ 创建时间  │ 2026-01-01            │  │
│ │ 家庭           │ │ │ 更新时间  │ 2026-03-01            │  │
│ │ ...            │ │ │ HTTP_PROXY│ http://...  ← 黄色    │  │
│ │ preset-list    │ │ │ 应用配置数│ 12                    │  │
│ │ item-h=40px    │ │ │                                   │  │
│ │                │ │ │ [应用] [设为默认] [导出] [删除]    │  │
│ └────────────────┘ │ └────────────────────────────────── ┘  │
│ [+ 新建预设]        │                                         │
│  w=210px           │  flex-grow:1                           │
├──────────────────────────────────────────────────────────────┤
│ 保存当前配置：  [输入预设名称______________]  [保存]           │
│  ● 反馈文字                                                  │
└──────────────────────────────────────────────────────────────┘

  ╔═ 新建预设弹窗（点[+ 新建预设]后显示）════════════════════╗
  ║  ┌── overlay-panel w=460px ────────────────────────────┐ ║
  ║  │  新建预设                                           │ ║
  ║  │  输入名称后将保存当前环境变量和工具代理配置           │ ║
  ║  │  预设名称    [如: office, home, project______]      │ ║
  ║  │  描述（可选）[描述此预设的使用场景___________]      │ ║
  ║  │                              [取消]  [创建]        │ ║
  ║  └───────────────────────────────────────────────────  ┘ ║
  ╚═══════════════════════════════════════════════════════════╝
```

**按钮交互**

| 按钮 | name | 点击后界面变化 |
|-----|------|---------------|
| + 新建预设 | `btn-new-preset` | new-preset-overlay 显示 |
| 应用 | `btn-apply-preset` | 加载选中预设配置；反馈绿色 |
| 设为默认 | `btn-set-default` | 列表项名称后追加"(默认)" |
| 导出 | `btn-export-preset` | 弹出文件保存对话框 |
| 删除 | `btn-delete-preset` | 列表移除该项；右侧详情清空 |
| 保存 | `btn-save-preset` | 将当前配置存为新预设；列表刷新 |
| 弹窗 取消 | `btn-new-cancel` | overlay display→none |
| 弹窗 创建 | `btn-new-save` | 保存新预设；列表刷新；关闭弹窗 |

---

### 1.5 状态总览 StatusPanel

**文件**：`Assets/UI/AIGate/StatusPanel.uxml`

```
panel-root (flex-column)
├── panel-header (flex-row)
│   ├── Label "状态总览"  20px bold
│   ├── Label "全局代理 + 应用配置 + 预设"  12px #475569
│   └── panel-header-actions → Button(btn-refresh-status) "刷新" .btn-secondary .btn-sm
├── status-card (全局代理)
│   ├── Label "全局代理（环境变量）"
│   ├── status-row → "HTTP_PROXY"  + Label(status-http)    + Button(btn-edit-http)    "编辑" .btn-inline
│   ├── status-row → "HTTPS_PROXY" + Label(status-https)   + Button(btn-edit-https)   "编辑"
│   └── status-row → "NO_PROXY"    + Label(status-noproxy) + Button(btn-edit-noproxy) "编辑"
├── status-card (应用代理)
│   ├── card-header-row → Label"应用代理配置" + Label(configured-count)"已配置 0 个" badge
│   └── ScrollView(status-tool-scroll) max-h:230px
│       └── VisualElement(status-tool-list) [C#动态生成: 分类标题+工具行]
│           分类标题: 11px bold color:#4f8ef7
│           工具行: ●点(8px) + 名称(w=150px) + 代理值(flex-grow 黄#f59e0b)
└── status-card--preset (flex-row align-center)
    ├── Label "当前预设"
    └── Label(current-preset-label) "(无)" 14px bold color:#4f8ef7
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐
│ 状态总览                                           [刷新]    │
├──────────────────────────────────────────────────────────────┤
│ ┌─ 全局代理（环境变量）────────────────────────────────────┐  │
│ │ HTTP_PROXY   │ (未设置)                     [编辑]      │  │
│ │ HTTPS_PROXY  │ (未设置)                     [编辑]      │  │
│ │ NO_PROXY     │ (未设置)                     [编辑]      │  │
│ └─────────────────────────────────────────────────────── ┘  │
│ ┌─ 应用代理配置  ●已配置 0 个 ─────────────────────────────┐  │
│ │ ── 版本控制 ──  ← 蓝色分类标题                          │  │
│ │ ● Git     http://proxy:7890                            │  │
│ │ ○ SVN     (未配置)                                     │  │
│ │ ── 包管理器 ──                                          │  │
│ │ ● npm     http://proxy:7890                            │  │
│ │  [可滚动 max-height:230px]                             │  │
│ └─────────────────────────────────────────────────────── ┘  │
│ ┌─ 当前预设 ────────────────────────────────────────────────┐ │
│ │ 当前预设  (无)  ← 蓝色粗体 14px                          │ │
│ └─────────────────────────────────────────────────────── ┘  │
└──────────────────────────────────────────────────────────────┘
```

**按钮交互**

| 按钮 | name | 点击后界面变化 |
|-----|------|---------------|
| 刷新 | `btn-refresh-status` | 重读所有数据；三张卡片全部更新 |
| 编辑(HTTP) | `btn-edit-http` | 切换到 GlobalPanel 聚焦 http-input |
| 编辑(HTTPS) | `btn-edit-https` | 切换到 GlobalPanel 聚焦 https-input |
| 编辑(NO_PROXY) | `btn-edit-noproxy` | 切换到 GlobalPanel 聚焦 noproxy-input |

---

### 1.6 连通性测试 TestPanel

**文件**：`Assets/UI/AIGate/TestPanel.uxml`

```
panel-root (flex-column)
├── panel-header
│   ├── Label "连通性测试"  20px bold
│   └── Label "Proxy Connectivity Test --verify"  12px #475569
├── form-card (bg:#111827 r:10px p:16px 20px mb:14px)
│   ├── form-row: Label"代理地址" + TextField(test-proxy-input)
│   │            + Button(btn-use-current)"使用当前" .btn-secondary .btn-sm
│   └── form-row: Label"测试URL" + TextField(test-url-input)
│                 placeholder"留空使用默认 http://www.google.com"
├── Button(btn-run-test) "开始测试" .btn-primary .btn-large h=44px
├── test-result-area (mt:14px)
│   ├── result-card--success (bg:#0a1f12 border:#166534 display:none初始)
│   │   ├── Label "OK  连接成功"  15px bold color:#22c55e
│   │   ├── Label(result-time) "响应时间: - ms"
│   │   └── Label(result-url)  "目标: -"
│   └── result-card--fail (bg:#1a0a0a border:#7f1d1d display:none初始)
│       ├── Label "ERR  连接失败"  15px bold color:#f87171
│       └── Label(result-error)  ""
└── status-card (测试历史)
    ├── Label "测试历史"  11px bold #475569
    └── ListView(test-history-list) item-height=44px max-height:180px
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐
│ 连通性测试                                                   │
│ Proxy Connectivity Test --verify                            │
├──────────────────────────────────────────────────────────────┤
│ ┌─ form-card ─────────────────────────────────────────────┐  │
│ │ 代理地址 [http://host:port_______]  [使用当前]           │  │
│ │ 测试URL  [留空使用默认 http://www.google.com________]    │  │
│ └─────────────────────────────────────────────────────── ┘  │
│ [              开始测试              ]  h=44px btn-primary    │
│                                                              │
│ ┌─ 成功卡（测试成功后 display:flex）──────────────────────┐  │
│ │ OK  连接成功                                           │  │
│ │ 响应时间: 342ms                                        │  │
│ │ 目标: http://www.google.com                           │  │
│ └─────────────────────────────────────────────────────── ┘  │
│ ┌─ 失败卡（测试失败后 display:flex）──────────────────────┐  │
│ │ ERR  连接失败                                          │  │
│ │ 错误信息内容                                           │  │
│ └─────────────────────────────────────────────────────── ┘  │
│ ┌─ 测试历史  max-height:180px ────────────────────────────┐  │
│ │ ●绿 14:23  http://proxy:7890  342ms  成功              │  │
│ │ ●红 14:20  http://proxy:7890  超时   失败              │  │
│ └─────────────────────────────────────────────────────── ┘  │
└──────────────────────────────────────────────────────────────┘
```

**按钮交互**

| 按钮 | name | 点击后界面变化 |
|-----|------|---------------|
| 使用当前 | `btn-use-current` | 读取 HTTP_PROXY 环境变量填入 test-proxy-input |
| 开始测试 | `btn-run-test` | 按钮→"测试中..."并禁用；两张结果卡全隐藏；完成后：成功→success卡显示，失败→fail卡显示；历史追加条目；按钮恢复 |

---

### 1.7 工具路径 ToolPathPanel

**文件**：`Assets/UI/AIGate/ToolPathPanel.uxml`

```
panel-root (flex-column)
├── panel-header (flex-row)
│   ├── Label "工具路径配置"  20px bold
│   ├── Label "自定义可执行文件路径 / 配置文件路径"  12px
│   └── panel-header-actions → Button(btn-reset-all) "重置全部" .btn-danger .btn-sm
├── status-card (说明)
│   ├── Label "说明"  11px bold
│   └── Label "如果工具安装在非标准路径...留空则使用自动检测。"
├── toolbar (flex-row)
│   ├── TextField(path-search)      flex-grow:1  placeholder"搜索工具..."
│   └── Toggle(custom-only-toggle)  label="仅显示已自定义"
├── list-header (flex-row)
│   ├── Label "工具名称"  w=180px  11px bold
│   ├── Label "状态"      w=80px
│   └── Label "操作"      w=130px 右对齐
├── ListView(path-tool-list)  item-height=44px  .gate-listview
│   └── 每行 (C# makeItem/bindItem):
│       ├── Label 工具名称  w=180px  bold
│       ├── VisualElement 状态点  绿=已自定义 灰=自动检测
│       └── Button "配置"  .btn-sm
├── Label(path-feedback)
└── path-edit-overlay (position:absolute 全覆盖 display:none)
    └── overlay-panel w=460px bg:#111827 r:14px p:28px 32px
        ├── Label(path-edit-title) "配置路径"  18px bold
        ├── Label "留空则恢复自动检测"
        ├── form-row: Label"可执行文件" + TextField(exec-path-input)
        ├── form-row: Label"配置文件"   + TextField(config-path-input)
        └── overlay-actions (flex-row justify:flex-end)
            ├── Button(btn-path-clear)  "清除"  .btn-danger
            ├── Button(btn-path-cancel) "取消"  .btn-secondary
            └── Button(btn-path-save)   "保存"  .btn-primary
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐
│ 工具路径配置                                    [重置全部]    │
│ 自定义可执行文件路径 / 配置文件路径                            │
├──────────────────────────────────────────────────────────────┤
│ ┌─ 说明 ─────────────────────────────────────────────────┐  │
│ │ 如果工具安装在非标准路径，可手动指定。留空则自动检测。    │  │
│ └─────────────────────────────────────────────────────── ┘  │
│ [搜索工具___________]  □ 仅显示已自定义                      │
├──────────────────────┬──────────┬────────────────────────────┤
│ 工具名称(w=180)      │ 状态     │ 操作(右对齐)               │
├──────────────────────────────────────────────────────────────┤
│ Git                  │ ●绿已定义│              [配置]        │
│ npm                  │ ○灰自动  │              [配置]        │
│ pip                  │ ○灰自动  │              [配置]        │
│  [ListView item-height=44px]                                 │
│  ● 反馈文字                                                  │
└──────────────────────────────────────────────────────────────┘

  ╔═ 路径编辑弹窗（点[配置]后显示）══════════════════════════╗
  ║  ┌── overlay-panel w=460px ────────────────────────────┐ ║
  ║  │  配置路径: {toolname}                               │ ║
  ║  │  留空则恢复自动检测                                  │ ║
  ║  │  可执行文件 [/usr/local/bin/mytool__________]       │ ║
  ║  │  配置文件   [~/.mytoolrc____________________]       │ ║
  ║  │            [清除]  [取消]  [保存]                  │ ║
  ║  └───────────────────────────────────────────────────  ┘ ║
  ╚═══════════════════════════════════════════════════════════╝
```

**按钮交互**

| 按钮 | name | 点击后界面变化 |
|-----|------|---------------|
| 重置全部 | `btn-reset-all` | 所有工具路径清空→自动检测；状态点全变灰 |
| 列表行 [配置] | — | path-edit-overlay 显示；标题→"配置路径: {toolname}" |
| 弹窗 清除 | `btn-path-clear` | 两个输入框清空 |
| 弹窗 取消 | `btn-path-cancel` | overlay display→none |
| 弹窗 保存 | `btn-path-save` | 写入路径配置；关闭弹窗；该行状态点→绿 |

---

## 二、CloudflareSeedTest-CSharp 项目

**主题**：GitHub 暗黑风  
**字体**：与系统默认字体（USS 未声明 -unity-font-definition，依赖 PanelSettings TextSettings）

| 变量 | 值 | 用途 |
|------|-----|------|
| bg | `#0d1117` | 根背景 |
| surface | `#161b22` | 卡片、标题栏 |
| border | `#30363d` | 通用边框 |
| border-dark | `#21262d` | 内部分隔线 |
| accent | `#58a6ff` | 选中、链接、进度条 |
| text-primary | `#c9d1d9` | 主文字 |
| text-muted | `#484f58` | 副标题、表头 |
| text-ip | `#79c0ff` | IP 地址列（蓝） |
| text-latency | `#d2a8ff` | 延迟列（紫） |
| text-speed | `#3fb950` | 速度列（绿） |
| btn-primary | `#238636` | 主按钮（绿） |
| danger | `#f85149` | 危险操作 |

---

### 2.1 主窗口 CfstMainWindow

**文件**：`Assets/UI/CloudflareST/CfstMainWindow.uxml`

```
cfst-root (flex-column min-w:640px min-h:400px bg:#0d1117)
├── cfst-titlebar  h=44px bg:#161b22 border-bottom:1px #30363d
│   ├── Label "CloudflareST"            14px bold color:#58a6ff
│   ├── Label "Cloudflare IP 优选工具"   11px color:#484f58
│   ├── spacer flex-grow:1
│   └── Label(cfst-version-label) "v2.0" badge bg:#1c2128 border:1px #30363d r:10px
└── cfst-body (flex-row flex-grow:1)
    ├── cfst-sidebar  w=156px bg:#0d1117 border-right:1px #21262d
    │   ├── Button cfst-nav-config   "⚙ 配置"   .cfst-nav-item--active
    │   ├── Button cfst-nav-run      "▶ 测速"   .cfst-nav-item
    │   ├── Button cfst-nav-result   "★ 结果"   .cfst-nav-item
    │   ├── Button cfst-nav-schedule "◷ 调度"   .cfst-nav-item
    │   ├── Button cfst-nav-history  "☰ 历史"   .cfst-nav-item
    │   ├── divider h=1px bg:#21262d margin:6px 10px
    │   └── Button cfst-nav-about    "i 关于"   .cfst-nav-item
    └── cfst-content  flex-grow:1 bg:#0d1117
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐  h=44px bg:#161b22
│  CloudflareST   Cloudflare IP 优选工具              [v2.0]  │
│  蓝14px bold    灰11px                              灰badge  │
├───────────┬──────────────────────────────────────────────────┤
│           │                                                  │
│ ⚙ 配置    │← 选中: bg:#1f3051 color:#58a6ff bold            │
│ ▶ 测速    │  普通: color:#8b949e h=36px r:6px margin:1px 4px│
│ ★ 结果    │  hover: bg:#161b22 color:#c9d1d9               │
│ ◷ 调度    │                                                  │
│ ☰ 历史    │        cfst-content                            │
│ ─────────  │        bg:#0d1117                              │
│ i 关于    │        C# 动态加载各面板                         │
│           │                                                  │
│  w=156px  │  flex-grow:1                                    │
└───────────┴──────────────────────────────────────────────────┘
```

**导航按钮交互**

| 按钮 | name | 加载面板 | 内容区变化 |
|-----|------|---------|----------|
| ⚙ 配置 | `cfst-nav-config` | `CfstConfigPanel.uxml` | 可滚动配置表单（7张卡片） |
| ▶ 测速 | `cfst-nav-run` | `CfstRunPanel.uxml` | 配置摘要 + 进度条 + 实时日志 |
| ★ 结果 | `cfst-nav-result` | `CfstResultPanel.uxml` | 汇总卡 + 表头 + 结果ListView |
| ◷ 调度 | `cfst-nav-schedule` | `CfstSchedulePanel.uxml` | 调度模式配置 + Hosts更新 + 状态 |
| ☰ 历史 | `cfst-nav-history` | `CfstHistoryPanel.uxml` | 历史表格 + 选中详情卡 |
| i 关于 | `cfst-nav-about` | `CfstAboutPanel.uxml` | 项目信息 + 功能 + 开源协议 |

---

### 2.2 测速配置 CfstConfigPanel

**文件**：`Assets/UI/CloudflareST/CfstConfigPanel.uxml`

```
cfst-panel-root (flex-column padding:16px 18px)
├── cfst-panel-header (固定不滚动)
│   ├── Label "测速配置"  16px bold color:#f0f6fc
│   └── Label "Speed Test Configuration"  11px #484f58
└── ScrollView (flex-grow:1 mode:Vertical)
    └── cfst-panel-inner (flex-column)
        ├── cfst-card「测速协议」
        │   ├── Toggle(cfg-use-icmp)    "ICMP Ping（默认）"
        │   ├── Toggle(cfg-use-tcping)  "TCPing（TCP 443，无需 ICMP 权限）"
        │   └── Toggle(cfg-use-httping) "HTTPing（可过滤地区码）"
        ├── cfst-card「并发与精度」
        │   ├── form-row: Label"延迟并发数 -n" + TextField(cfg-concurrency) value=200 + hint"推荐 200"
        │   ├── form-row: Label"测速次数 -t"   + TextField(cfg-runs-per-ip) value=4   + hint"单IP测速次数"
        │   └── form-row: Label"IP数上限 -ipn" + TextField(cfg-ip-limit)    value=0   + hint"0=不限"
        ├── cfst-card「测速地址」
        │   ├── form-row: Label"URL -url" + TextField(cfg-url) value=https://speed.cloudflare.com/...
        │   └── form-row: Label"端口 -tp" + TextField(cfg-tp) value=443 + hint"HTTP=80 HTTPS=443"
        ├── cfst-card「IP来源」
        │   ├── form-row: Label"IPv4文件 -f" + TextField(cfg-ip-file) value=ip.txt + Button"浏览"
        │   ├── Toggle(cfg-use-ipv6) "启用 IPv6 (-ipv6)"
        │   └── form-row: Label"IPv6文件 -f6" + TextField(cfg-ipv6-file) value=ipv6.txt
        ├── cfst-card「下载测速」
        │   ├── Toggle(cfg-download-enabled) "启用下载测速"
        │   ├── form-row: Label"测速IP数 -dn" + TextField(cfg-dn) value=10
        │   ├── form-row: Label"超时(秒) -dt" + TextField(cfg-dt) value=10
        │   └── form-row: Label"速度下限 -sl" + TextField(cfg-sl) value=0 + hint"MB/s 0=不限"
        ├── cfst-card「输出」
        │   ├── form-row: Label"CSV文件 -o"   + TextField(cfg-output-file) + Button"浏览"
        │   ├── form-row: Label"输出数量 -p"  + TextField(cfg-output-limit) value=10
        │   ├── Toggle(cfg-silent) "-q 静默模式"
        │   └── Toggle(cfg-debug)  "-debug 调试输出"
        ├── cfst-card「Hosts更新」
        │   ├── form-row: Label"域名 -hosts" + TextField(cfg-hosts-expr) placeholder"cdn.example.com"
        │   └── Toggle(cfg-hosts-dry-run) "仅预览，不写入"
        └── Label(cfg-feedback)
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐
│ 测速配置                                                     │  固定标题
│ Speed Test Configuration                                    │
├──────────────────────────────────────────────────────────────┤
│ ↕ 可滚动区域                                                 │
│ ┌─ 测速协议 ───────────────────────────────────────────────┐ │
│ │ □ ICMP Ping（默认）                                     │ │
│ │ □ TCPing（TCP 443，无需 ICMP 权限）                     │ │
│ │ □ HTTPing（可过滤地区码）                               │ │
│ └───────────────────────────────────────────────────────── ┘ │
│ ┌─ 并发与精度 ──────────────────────────────────────────────┐ │
│ │ 延迟并发数 -n  [200_]  推荐 200                         │ │
│ │ 测速次数 -t    [4___]  单IP测速次数                     │ │
│ │ IP数上限 -ipn  [0___]  0=不限                           │ │
│ └───────────────────────────────────────────────────────── ┘ │
│ ┌─ 测速地址 ────────────────────────────────────────────────┐ │
│ │ URL -url  [https://speed.cloudflare.com/__down?bytes=...] │ │
│ │ 端口 -tp  [443_]  HTTP=80 HTTPS=443                     │ │
│ └───────────────────────────────────────────────────────── ┘ │
│ ┌─ IP来源 ──────────────────────────────────────────────────┐ │
│ │ IPv4文件 -f  [ip.txt___]  [浏览]                        │ │
│ │ □ 启用 IPv6 (-ipv6)                                     │ │
│ │ IPv6文件 -f6 [ipv6.txt_]                                │ │
│ └───────────────────────────────────────────────────────── ┘ │
│ ┌─ 下载测速 ────────────────────────────────────────────────┐ │
│ │ ■ 启用下载测速                                          │ │
│ │ 测速IP数 -dn  [10__]                                    │ │
│ │ 超时(秒) -dt  [10__]                                    │ │
│ │ 速度下限 -sl  [0___]  MB/s 0=不限                       │ │
│ └───────────────────────────────────────────────────────── ┘ │
│ ┌─ 输出 ────────────────────────────────────────────────────┐ │
│ │ CSV文件 -o   [result.csv___]  [浏览]                    │ │
│ │ 输出数量 -p  [10___]                                    │ │
│ │ □ 静默模式 -q    □ 调试输出 -debug                      │ │
│ └───────────────────────────────────────────────────────── ┘ │
│ ┌─ Hosts更新 ───────────────────────────────────────────────┐ │
│ │ 域名 -hosts  [cdn.example.com___________]               │ │
│ │ □ 仅预览，不写入                                        │ │
│ └───────────────────────────────────────────────────────── ┘ │
└──────────────────────────────────────────────────────────────┘
```

所有 TextField 样式：bg:#0d1117 border:1px #30363d r:6px h=28px；focus: border:#58a6ff。  
Toggle 样式：16×16px r:4px；选中: bg:#1f3051 border:#58a6ff checkmark:#58a6ff。  
[浏览] 按钮：.cfst-btn-secondary .cfst-btn-sm h=26px。

---

### 2.3 测速进度 CfstRunPanel

**文件**：`Assets/UI/CloudflareST/CfstRunPanel.uxml`

```
cfst-panel-root (flex-column)
├── cfst-panel-header (固定 flex-row align-center)
│   ├── Label "测速进度"  16px bold
│   ├── Label(cfst-run-status-badge) "空闲"  .cfst-badge--idle
│   └── cfst-panel-header-right (flex-grow:1 flex-row justify:flex-end)
│       ├── Button(run-btn-start)  "开始测速"  .cfst-btn-primary
│       └── Button(run-btn-cancel) "取消"      .cfst-btn-danger
└── ScrollView (flex-grow:1)
    └── cfst-panel-inner
        ├── cfst-card「当前配置」
        │   ├── form-row: Label"协议"   + Label(run-info-protocol)    "ICMP Ping"
        │   ├── form-row: Label"并发"   + Label(run-info-concurrency) "200"
        │   ├── form-row: Label"IP来源" + Label(run-info-ip-source)   "ip.txt"
        │   └── form-row: Label"测速URL"+ Label(run-info-url)         "—"
        ├── cfst-card「进度」
        │   ├── form-row: Label"延迟测速" + Label(run-ping-count) "0 / 0"
        │   ├── progress-track(run-ping-track) h=6px bg:#21262d r:3px
        │   │   └── progress-fill(run-ping-fill) w=0 bg:#58a6ff
        │   ├── form-row: Label"下载测速" + Label(run-dl-count) "0 / 0"
        │   ├── progress-track(run-dl-track)
        │   │   └── progress-fill(run-dl-fill)
        │   └── form-row: Label"已用时间" + Label(run-elapsed) "0s"
        │              + spacer + Label"当前IP" + Label(run-current-ip) "—"
        └── cfst-card「实时日志」
            └── ScrollView(run-log-scroll) min-h:80px max-h:160px bg:#010409
                └── Label(run-log-text) "等待开始..."  12px #8b949e
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐
│ 测速进度  [空闲]                    [开始测速]  [取消]        │  固定标题
├──────────────────────────────────────────────────────────────┤
│ ┌─ 当前配置 ───────────────────────────────────────────────┐ │
│ │ 协议    │ ICMP Ping                                     │ │
│ │ 并发    │ 200                                           │ │
│ │ IP来源  │ ip.txt                                        │ │
│ │ 测速URL │ https://speed.cloudflare.com/...              │ │
│ └───────────────────────────────────────────────────────── ┘ │
│ ┌─ 进度 ────────────────────────────────────────────────────┐ │
│ │ 延迟测速  1234 / 5000                                   │ │
│ │ ████████████░░░░░░░░░░░  h=6px bg:#58a6ff              │ │
│ │ 下载测速  3 / 10                                        │ │
│ │ ████░░░░░░░░░░░░░░░░░░░                                │ │
│ │ 已用时间  23s          当前IP  104.16.1.1              │ │
│ └───────────────────────────────────────────────────────── ┘ │
│ ┌─ 实时日志 max-h:160px bg:#010409 ────────────────────────┐ │
│ │ [14:23:01] 开始测速，共 5000 个 IP...                   │ │
│ │ [14:23:02] 104.16.1.1  延迟 45ms  丢包 0%             │ │
│ │ [14:23:03] 104.16.2.1  延迟 89ms  丢包 0%             │ │
│ │  ↕ 可滚动                                               │ │
│ └───────────────────────────────────────────────────────── ┘ │
└──────────────────────────────────────────────────────────────┘
```

**状态徽章变化**

| 状态 | badge class | 文字 | 颜色 |
|------|------------|------|------|
| 空闲 | `cfst-badge--idle` | 空闲 | 灰 #484f58 |
| 运行中 | `cfst-badge--running` | 运行中 | 蓝 #58a6ff |
| 完成 | `cfst-badge--done` | 完成 | 绿 #3fb950 |
| 出错 | `cfst-badge--error` | 出错 | 红 #f85149 |

**按钮交互**

| 按钮 | name | 点击后界面变化 |
|-----|------|---------------|
| 开始测速 | `run-btn-start` | badge→"运行中"；进度条开始动画；日志开始追加；取消按钮激活 |
| 取消 | `run-btn-cancel` | 停止测速；badge→"空闲"；进度条保持当前值 |

---

### 2.4 测速结果 CfstResultPanel

**文件**：`Assets/UI/CloudflareST/CfstResultPanel.uxml`

```
cfst-panel-root (flex-column)
├── cfst-panel-header (固定 flex-row)
│   ├── Label "测速结果"  16px bold
│   ├── Label(result-count-badge) "0 条"  .cfst-badge--idle
│   └── cfst-panel-header-right
│       ├── Button(result-btn-copy)   "复制 IP"   .cfst-btn-secondary .cfst-btn-sm
│       ├── Button(result-btn-export) "导出 CSV"  .cfst-btn-secondary .cfst-btn-sm
│       └── Button(result-btn-clear)  "清空"      .cfst-btn-danger    .cfst-btn-sm
├── cfst-card「测速汇总」(固定)
│   ├── form-row: Label"测试时间" + Label(result-test-time) + spacer + Label"用时" + Label(result-duration)
│   ├── form-row: Label"测试IP数" + Label(result-total-ips) + spacer + Label"有效" + Label(result-valid-count) badge
│   └── form-row: Label"最快IP"   + Label(result-best-ip)  + spacer + Label"最低延迟" + Label(result-best-latency)
├── cfst-table-header (固定 flex-row bg:#1c2128 r:6px6px00)
│   ├── Label "#"    .cfst-td--rank    w=30px
│   ├── Label "IP"   .cfst-td--ip      w=140px  color:#79c0ff
│   ├── Label "丢包" .cfst-td--loss    w=56px
│   ├── Label "延迟" .cfst-td--latency w=76px   color:#d2a8ff
│   ├── Label "速度" .cfst-td--speed   w=86px   color:#3fb950
│   └── Label "地区" .cfst-td--colo    flex-grow:1
└── ListView(result-list)  item-height=38px  .cfst-table-body  flex-grow:1
    └── 每行 .cfst-table-row (C# makeItem/bindItem):
        ├── Label 序号   w=30px  color:#484f58
        ├── Label IP     w=140px color:#79c0ff bold
        ├── Label 丢包率  w=56px  居中
        ├── Label 延迟   w=76px  color:#d2a8ff 右对齐
        ├── Label 速度   w=86px  color:#3fb950 右对齐
        └── Label 地区   flex-grow:1  color:#8b949e
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐
│ 测速结果  [10条]          [复制IP]  [导出CSV]  [清空]         │  固定
├──────────────────────────────────────────────────────────────┤
│ ┌─ 测速汇总 ───────────────────────────────────────────────┐ │  固定
│ │ 测试时间 2026-03-11 14:23   用时  45s                   │ │
│ │ 测试IP数 5000               有效  [10] ← 绿badge        │ │
│ │ 最快IP   104.16.1.1         最低延迟  45ms ← 紫色        │ │
│ └───────────────────────────────────────────────────────── ┘ │
├────┬──────────────┬──────┬────────┬────────┬─────────────────┤  固定表头
│ #  │ IP           │ 丢包 │ 延迟   │ 速度   │ 地区            │
│    │ 蓝色         │      │ 紫色   │ 绿色   │                 │
├────┴──────────────┴──────┴────────┴────────┴─────────────────┤
│  1 │ 104.16.1.1   │  0%  │  45ms  │ 82Mbps │ HKG 香港        │  ↕ 滚动
│  2 │ 104.16.2.1   │  0%  │  52ms  │ 75Mbps │ NRT 东京        │
│  3 │ 104.16.3.1   │  0%  │  67ms  │ 68Mbps │ LAX 洛杉矶      │
│  ... ListView item-height=38px  flex-grow:1                  │
└──────────────────────────────────────────────────────────────┘
```

**按钮交互**

| 按钮 | name | 点击后界面变化 |
|-----|------|---------------|
| 复制 IP | `result-btn-copy` | 将结果列表所有 IP 复制到剪贴板 |
| 导出 CSV | `result-btn-export` | 弹出文件保存对话框，写入 CSV |
| 清空 | `result-btn-clear` | ListView 清空；汇总卡重置为 "—"；badge→"0 条" |

---

### 2.5 历史记录 CfstHistoryPanel

**文件**：`Assets/UI/CloudflareST/CfstHistoryPanel.uxml`

```
cfst-panel-root (flex-column)
├── cfst-panel-header (固定 flex-row)
│   ├── Label "历史记录"  16px bold
│   ├── Label(history-count-badge) "0 条"  .cfst-badge--idle
│   └── cfst-panel-header-right
│       └── Button(history-btn-clear) "清空历史"  .cfst-btn-danger .cfst-btn-sm
├── cfst-table-header (固定 flex-row bg:#1c2128)
│   ├── Label "时间"  .cfst-history-time    w=120px
│   ├── Label "模式"  .cfst-history-mode    w=64px
│   ├── Label "摘要"  .cfst-history-summary flex-grow:1
│   └── Label "状态"  .cfst-history-status  w=52px 右对齐
├── ListView(history-list)  item-height=38px  .cfst-table-body  flex-grow:1
│   └── 每行 .cfst-history-row (C# makeItem/bindItem):
│       ├── Label 时间   w=120px  11px color:#484f58
│       ├── Label 模式   w=64px   12px color:#8b949e
│       ├── Label 摘要   flex-grow:1  12px color:#c9d1d9
│       └── Label 状态   w=52px  右对齐  成功绿/失败红
└── cfst-card「选中记录详情」(固定在底部)
    ├── Label "选中记录详情"  10px bold #484f58
    ├── form-row: Label"时间"   + Label(detail-time)
    ├── form-row: Label"协议"   + Label(detail-protocol)
    ├── form-row: Label"用时"   + Label(detail-duration)
    ├── form-row: Label"最佳IP" + Label(detail-best-ip)  color:#79c0ff
    ├── form-row: Label"摘要"   + Label(detail-summary)
    └── cfst-action-row → Button(detail-btn-rerun) "重新测速" .cfst-btn-secondary .cfst-btn-sm
```

**布局图**

```
┌──────────────────────────────────────────────────────────────┐
│ 历史记录  [5条]                              [清空历史]       │  固定
├────────────────┬────────┬───────────────────────┬────────────┤  固定表头
│ 时间(w=120)    │ 模式   │ 摘要                  │ 状态       │
├──────────────────────────────────────────────────────────────┤
│ 2026-03-11 14:23│ ICMP  │ 最快 104.16.1.1 45ms  │ ✓成功      │  ↕ 滚动
│ 2026-03-11 12:00│ TCP   │ 最快 104.16.2.1 52ms  │ ✓成功      │
│ 2026-03-10 18:30│ ICMP  │ 0 结果                │ ✗失败      │
│  ListView item-height=38px  flex-grow:1                      │
├──────────────────────────────────────────────────────────────┤
│ ┌─ 选中记录详情 ────────────────────────────────────────────┐ │  固定底部
│ │ 时间    2026-03-11 14:23                               │ │
│ │ 协议    ICMP Ping                                      │ │
│ │ 用时    45s                                            │ │
│ │ 最佳IP  104.16.1.1  ← 蓝色                            │ │
│ │ 摘要    共10条，最低延迟45ms，最高速度82Mbps            │ │
│ │                                      [重新测速]        │ │
│ └───────────────────────────────────────────────────────── ┘ │
└──────────────────────────────────────────────────────────────┘
```

**按钮交互**

| 按钮 | name | 点击后界面变化 |
|-----|------|---------------|
| 清空历史 | `history-btn-clear` | ListView 清空；badge→"0 条"；详情卡重置 |
| ListView 点击行 | — | 底部详情卡各字段更新为选中记录数据 |
| 重新测速 | `detail-btn-rerun` | 切换到测速面板，自动填入历史记录的配置并开始测速 |
