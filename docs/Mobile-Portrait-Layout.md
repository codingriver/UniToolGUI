# CFST GUI — 移动手机版竖屏界面布局文档

> **适用平台**：Android / iOS 手机竖屏（Portrait）
> **参考分辨率**：360 × 800 dp，适配 720×1600、1080×2400 等主流机型
> **基准文档**：`GUI-Layout-Design.md`（桌面版设计文档）
> **界面框架**：Unity UIToolkit（UXML + USS）
> **布局策略**：底部 Tab 导航 + 全屏内容区 + 顶部操作栏，替换桌面版左侧固定导航栏
> **路径策略**：所有文件路径固定使用 `Application.persistentDataPath`，界面仅展示只读 Label，不可手动修改

---

## 一、整体布局结构

### 1.1 竖屏总体框架

桌面版采用「左侧固定导航栏（192px）+ 右侧内容区」横向分栏，在手机竖屏下不适用：
- 192px 侧边栏在 360dp 宽屏幕上占比超过 53%，内容区极度压缩
- 导航菜单项触摸区域不足（最小 44dp 要求）

**竖屏改造方案**：侧边导航改为**底部 Tab 栏**，主内容区全宽显示。

```
+--------------------------------------+
| 顶部操作栏  56dp                      |
| [☁ CFST]           [Start] [Stop]   |
+--------------------------------------+
|                                      |
|        主内容区 (flex-grow:1)         |
|        随底部Tab切换，可竖向滚动       |
|                                      |
+--------------------------------------+
| 进度条区  32dp（运行时出现）           |
| [=============....] 62% 延迟测速中   |
+--------------------------------------+
| 底部Tab导航  60dp                     |
| [来源][延迟][下载][结果][调度][更多]  |
+--------------------------------------+
| 底部状态栏  24dp                      |
| 已测:486/2000 | 耗时:01:31 | 50ms    |
+--------------------------------------+
```

### 1.2 分区高度分配

| 区域 | 高度 | flex 属性 | 说明 |
|------|------|-----------|------|
| 顶部操作栏 | 56dp | flex-shrink:0 | Logo + 开始/停止按钮 |
| 主内容区 | 自适应 | flex-grow:1 | 当前页面内容，可滚动 |
| 进度条区域 | 32dp | flex-shrink:0 | 运行时 display:flex，空闲 display:none |
| 底部 Tab 导航 | 60dp | flex-shrink:0 | 6 个主要页面入口 |
| 底部状态栏 | 24dp | flex-shrink:0 | 实时状态摘要 |

### 1.3 主 UXML 根结构

```xml
<ui:VisualElement name="m-root" class="m-root">

    <!-- 顶部操作栏 -->
    <ui:VisualElement name="m-topbar" class="m-topbar">
        <ui:VisualElement class="m-topbar__brand">
            <ui:Label text="☁" class="m-logo-icon" />
            <ui:Label text="CFST" class="m-logo-text" />
        </ui:VisualElement>
        <ui:VisualElement class="m-topbar__actions">
            <ui:Button name="btn-start" text="Start" class="m-btn-start" />
            <ui:Button name="btn-stop"  text="Stop"  class="m-btn-stop m-btn-stop--disabled" />
        </ui:VisualElement>
    </ui:VisualElement>

    <!-- 主内容区 -->
    <ui:VisualElement name="m-page-container" class="m-page-container">
        <!-- 各页面 Instance，同时只显示一个 -->
    </ui:VisualElement>

    <!-- 进度条区域（运行时出现）-->
    <ui:VisualElement name="m-progress-area" class="m-progress-area">
        <ui:VisualElement class="m-progress-row">
            <ui:VisualElement class="m-progress-track">
                <ui:VisualElement name="progress-fill" class="m-progress-fill" />
            </ui:VisualElement>
            <ui:Label name="progress-pct" text="0%" class="m-progress-pct" />
        </ui:VisualElement>
        <ui:Label name="status-text" text="就绪" class="m-status-text" />
    </ui:VisualElement>

    <!-- 底部 Tab 导航 -->
    <ui:VisualElement name="m-tabbar" class="m-tabbar">
        <ui:Button name="tab-ip"       class="m-tab-item m-tab-item--active">
            <ui:Label text="IP"  class="m-tab-icon" />
            <ui:Label text="来源" class="m-tab-label" />
        </ui:Button>
        <ui:Button name="tab-latency"  class="m-tab-item">
            <ui:Label text="ms"  class="m-tab-icon" />
            <ui:Label text="延迟" class="m-tab-label" />
        </ui:Button>
        <ui:Button name="tab-download" class="m-tab-item">
            <ui:Label text="DL"  class="m-tab-icon" />
            <ui:Label text="下载" class="m-tab-label" />
        </ui:Button>
        <ui:Button name="tab-results"  class="m-tab-item">
            <ui:Label text="表"  class="m-tab-icon" />
            <ui:Label text="结果" class="m-tab-label" />
            <ui:Label name="result-badge" text="" class="m-result-badge m-result-badge--hidden" />
        </ui:Button>
        <ui:Button name="tab-schedule" class="m-tab-item">
            <ui:Label text="定"  class="m-tab-icon" />
            <ui:Label text="调度" class="m-tab-label" />
        </ui:Button>
        <ui:Button name="tab-more"     class="m-tab-item">
            <ui:Label text="···" class="m-tab-icon" />
            <ui:Label text="更多" class="m-tab-label" />
        </ui:Button>
    </ui:VisualElement>

    <!-- 底部状态栏 -->
    <ui:VisualElement name="m-statusbar" class="m-statusbar">
        <ui:Label name="sb-tested"  text="已测: --"  class="m-sb-item" />
        <ui:Label class="m-sb-sep">|</ui:Label>
        <ui:Label name="sb-elapsed" text="耗时: --"  class="m-sb-item" />
        <ui:Label class="m-sb-sep">|</ui:Label>
        <ui:Label name="sb-best"    text="最快: --"  class="m-sb-item" />
    </ui:VisualElement>

</ui:VisualElement>
```

---

## 二、导航映射：桌面版 → 手机版

桌面版共 9 个导航项（含关于），手机底部 Tab 最多放 6 项，超出部分归入「更多」抽屉。

| 桌面导航项 | 手机 Tab | 备注 |
|------------|---------|------|
| IP 来源 | Tab 1：来源 | 常用，直接暴露 |
| 延迟测速 | Tab 2：延迟 | 常用，直接暴露 |
| 下载测速 | Tab 3：下载 | 常用，直接暴露 |
| 测速结果 | Tab 4：结果 | 含角标，直接暴露 |
| 定时调度 | Tab 5：调度 | 中等频率，直接暴露 |
| Hosts 更新 | 更多 → 抽屉 | 低频，折叠 |
| 输出设置 | 更多 → 抽屉 | 低频，折叠 |
| 其他设置 | 更多 → 抽屉 | 低频，折叠 |
| 关于 | 更多 → 抽屉 | 低频，折叠 |

「更多」Tab 点击后从底部弹出抽屉面板（高度约 240dp），列出 Hosts、输出、其他、关于四项。

---

## 三、USS 样式总览

### 3.1 根容器与顶部操作栏

```css
/* 根容器 */
.m-root {
    flex-direction: column;
    width: 100%;
    height: 100%;
    background-color: #0f1117;
    color: #e8eaf0;
}

/* 顶部操作栏 */
.m-topbar {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    height: 56px;
    padding: 0 16px;
    background-color: #161b24;
    border-bottom-width: 1px;
    border-bottom-color: #252d3d;
    flex-shrink: 0;
}
.m-topbar__brand { flex-direction: row; align-items: center; }
.m-logo-icon     { font-size: 20px; color: #f48120; margin-right: 8px; }
.m-logo-text     { font-size: 17px; color: #e8eaf0; -unity-font-style: bold; letter-spacing: 2px; }
.m-topbar__actions { flex-direction: row; align-items: center; }

/* 开始按钮 */
.m-btn-start {
    background-color: #f48120;
    color: #0f1117;
    border-width: 0;
    border-radius: 6px;
    padding: 0 18px;
    font-size: 13px;
    -unity-font-style: bold;
    margin-left: 8px;
    height: 36px;
    min-width: 76px;
    -unity-text-align: middle-center;
}
.m-btn-start:active   { background-color: #f9a057; }
.m-btn-start:disabled { background-color: #3a4155; color: #5a6480; }

/* 停止按钮 */
.m-btn-stop {
    background-color: #c0334a;
    color: #e8eaf0;
    border-width: 0;
    border-radius: 6px;
    padding: 0 18px;
    font-size: 13px;
    margin-left: 8px;
    height: 36px;
    min-width: 76px;
    -unity-text-align: middle-center;
}
.m-btn-stop:active    { background-color: #e03d56; }
.m-btn-stop--disabled { background-color: #1e2535; color: #5a6480; }
```

### 3.2 进度条区域

```css
.m-progress-area {
    flex-direction: column;
    padding: 5px 16px 4px 16px;
    background-color: #0f1117;
    border-bottom-width: 1px;
    border-bottom-color: #252d3d;
    flex-shrink: 0;
    display: none;                    /* 空闲时隐藏 */
}
.m-progress-area--active { display: flex; }

.m-progress-row   { flex-direction: row; align-items: center; }
.m-progress-track {
    flex-grow: 1;
    height: 5px;
    background-color: #1e2535;
    border-radius: 3px;
    overflow: hidden;
    margin-right: 8px;
}
.m-progress-fill {
    height: 5px;
    background-color: #f48120;
    border-radius: 3px;
    width: 0%;
    transition: width 0.3s;
}
.m-progress-pct  { font-size: 11px; color: #9aa3b5; min-width: 32px; -unity-text-align: middle-right; }
.m-status-text   { font-size: 11px; color: #9aa3b5; margin-top: 2px; }
```

### 3.3 底部 Tab 导航栏

```css
.m-tabbar {
    flex-direction: row;
    height: 60px;
    background-color: #161b24;
    border-top-width: 1px;
    border-top-color: #252d3d;
    flex-shrink: 0;
}
.m-tab-item {
    flex-grow: 1;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    background-color: transparent;
    border-width: 0;
    border-radius: 0;
    color: #9aa3b5;
    padding: 6px 0 4px 0;
    position: relative;
}
.m-tab-item:active      { background-color: rgba(244,129,32,0.08); }
.m-tab-item--active     { color: #f48120; border-top-width: 2px; border-top-color: #f48120; padding-top: 4px; }
.m-tab-icon             { font-size: 16px; -unity-text-align: middle-center; margin-bottom: 2px; }
.m-tab-label            { font-size: 10px; -unity-text-align: middle-center; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 48px; }

/* 结果角标 */
.m-result-badge {
    position: absolute;
    top: 4px; right: 8px;
    background-color: #f48120;
    color: #0f1117;
    font-size: 9px;
    -unity-font-style: bold;
    border-radius: 7px;
    padding: 1px 5px;
    min-width: 16px;
    -unity-text-align: middle-center;
}
.m-result-badge--hidden { display: none; }
```

### 3.4 底部状态栏

```css
.m-statusbar {
    flex-direction: row;
    align-items: center;
    justify-content: center;
    height: 24px;
    background-color: #161b24;
    border-top-width: 1px;
    border-top-color: #252d3d;
    padding: 0 12px;
    flex-shrink: 0;
}
.m-sb-item {
    font-size: 10px;
    color: #9aa3b5;
    flex-shrink: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
.m-sb-sep {
    font-size: 10px;
    color: #303a50;
    margin: 0 6px;
    flex-shrink: 0;
}
```

### 3.5 主内容区与页面容器

```css
.m-page-container {
    flex-grow: 1;
    position: relative;
    background-color: #0f1117;
}
.m-page {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    display: none;
    padding: 12px 16px 8px 16px;
    overflow: hidden;
}
.m-page--active {
    display: flex;
    flex-direction: column;
    align-items: stretch;
}
.m-page-title   { font-size: 16px; color: #e8eaf0; -unity-font-style: bold; margin-bottom: 4px; flex-shrink: 0; }
.m-page-divider { height: 1px; background-color: #252d3d; margin-bottom: 12px; flex-shrink: 0; }
.m-page-scroll  { flex-grow: 1; }
```

### 3.6 公共组件（手机版触摸放大）

桌面版最小高度 30px，手机版统一放大到 36px，标签宽度从 120px 缩短到 80px。

```css
/* 分组框 */
.m-group-box {
    border-width: 1px;
    border-color: #252d3d;
    border-radius: 8px;
    padding: 12px 14px;
    margin-bottom: 12px;
    background-color: #161b24;
}
.m-group-title {
    font-size: 11px;
    color: #f48120;
    -unity-font-style: bold;
    margin-bottom: 10px;
    letter-spacing: 1px;
}
/* 表单行 */
.m-form-row   { flex-direction: row; align-items: center; margin-bottom: 12px; flex-wrap: wrap; }
.m-form-col   { flex-direction: column; margin-bottom: 12px; }
.m-form-label { font-size: 12px; color: #9aa3b5; min-width: 80px; margin-right: 8px; -unity-text-align: middle-left; flex-shrink: 0; }
.m-form-hint  { font-size: 11px; color: #5a6480; margin-top: 3px; white-space: normal; }
/* TextField */
.m-field-text { flex-grow: 1; height: 36px; border-width: 1px; border-color: #303a50; border-radius: 5px; background-color: #1e2535; }
.m-field-text > .unity-base-text-field__input { background-color: #1e2535; border-width: 0; border-radius: 5px; color: #e8eaf0; font-size: 13px; padding: 4px 10px; -unity-text-align: middle-left; margin: 0; }
.m-field-text--multiline { height: 80px; }
.m-field-text--multiline > .unity-base-text-field__input { white-space: normal; }
/* IntegerField / FloatField */
.m-field-int { width: 100px; height: 36px; border-width: 1px; border-color: #303a50; border-radius: 5px; background-color: #1e2535; }
.m-field-int > .unity-base-text-field__input, .m-field-int > .unity-base-field__input { background-color: #1e2535; border-width: 0; border-radius: 5px; color: #e8eaf0; font-size: 13px; padding: 4px 8px; margin: 0; }
.m-field-unit { font-size: 11px; color: #5a6480; margin-left: 6px; }
/* Toggle */
.m-field-toggle { flex-direction: row; align-items: center; }
.m-field-toggle > .unity-toggle__input > .unity-toggle__checkmark { background-color: #1e2535; background-image: none; border-width: 1px; border-color: #303a50; border-radius: 3px; width: 18px; height: 18px; }
.m-field-toggle > .unity-toggle__input:checked > .unity-toggle__checkmark { background-color: #f48120; background-image: none; border-color: #f48120; }
.m-field-toggle .unity-label { font-size: 13px; color: #e8eaf0; }
/* 只读路径 Label */
.field-path-readonly { flex-grow: 1; font-size: 11px; color: #5a6480; background-color: #0a0d13; border-width: 1px; border-color: #1e2535; border-radius: 5px; padding: 5px 10px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; -unity-text-align: middle-left; margin-top: 4px; }
```

---

## 四、各页面竖屏布局详述

### 4.1 页面 1 — IP 来源

**对应桌面页**：页面 1 — IP 来源
**路径显示**：IPv4/IPv6 文件路径为只读 Label，由代码写入 `Application.persistentDataPath`，用户不可修改

```
+--------------------------------------+
| IP 来源                               |
| ------------------------------------ |
| [IP 文件]                             |
|  IPv4 文件 (-f)                       |
|  /data/.../ip.txt           [只读]   |
|  IPv6 文件 (-f6)                      |
|  /data/.../ipv6.txt         [只读]   |
|                                      |
| [直接指定 IP 段 (-ip)]                |
|  逗号分隔 CIDR，优先级高于文件        |
|  [173.245.48.0/20,104.16.0.0/13    ] |
|  [                                 ] |
|                                      |
| [加载选项]                            |
|  IP数量上限(-ipn)  [   0  ] 0=不限   |
|  □ 全量扫描(-allip)                   |
+--------------------------------------+
```

**手机版适配说明**：
- IPv4/IPv6 文件路径为只读 Label（样式 `field-path-readonly`），路径由 C# 代码在 `Awake` 阶段写入 `Application.persistentDataPath`
- IP 段文本框（多行，高度 80dp）仍可编辑，方便手动粘贴 CIDR
- 全量扫描 Toggle 触摸区最小 44dp

### 4.2 页面 2 — 延迟测速

**对应桌面页**：页面 2 — 延迟测速
**无路径字段**

```
+--------------------------------------+
| 延迟测速                              |
| ------------------------------------ |
| [测速方式]                            |
|  ◉ ICMP Auto（默认）                  |
|  ○ TCPing (-tcping)                  |
|  ○ HTTPing (-httping)                |
|  □ 强制 ICMP，禁止自动降级           |
|                                      |
| [并发与次数]                          |
|  并发数(-n)       [  200  ] 1~1000   |
|  单IP测速次数(-t) [   4   ] 1~20     |
|                                      |
| [过滤阈值]                            |
|  延迟上限(-tl)    [ 9999  ] ms       |
|  延迟下限(-tll)   [   0   ] ms       |
|  丢包率上限(-tlr) [  100  ] %        |
|                                      |
| [HTTPing 专属参数]（HTTPing 模式可见）|
|  有效状态码  [  0  ] 0=200/301/302   |
|  地区码过滤  [HKG,NRT,LAX          ] |
+--------------------------------------+
```

**手机版适配说明**：
- RadioButton 组纵向排列，每项触摸高度 44dp
- HTTPing 专属分组默认折叠，选中 HTTPing 后展开
- 数字输入框宽 100dp，右侧单位标签缩短

### 4.3 页面 3 — 下载测速

**对应桌面页**：页面 3 — 下载测速
**无路径字段**

```
+--------------------------------------+
| 下载测速                              |
| ------------------------------------ |
| □ 禁用下载测速(-dd) — 只测延迟       |
|                                      |
| [测速参数]                            |
|  测速URL(-url)                        |
|  [https://speed.cloudflare.com/...  ]|
|  测速端口(-tp)   [ 443 ] HTTPS=443   |
|  参与IP数(-dn)   [  10 ] 前N个IP     |
|  下载超时(-dt)   [  10 ] 秒          |
|  速度下限(-sl)   [   0 ] MB/s        |
+--------------------------------------+
```

**手机版适配说明**：
- URL 输入框全宽，height 36dp
- 禁用下载 Toggle 位于组顶部，勾选时参数区整体 opacity 降低
- 端口/IP数/超时/速度下限输入框排成两列（`flex-wrap:wrap`）

### 4.4 页面 4 — 定时调度

**对应桌面页**：页面 4 — 定时调度
**无路径字段**

```
+--------------------------------------+
| 定时调度                              |
| ------------------------------------ |
| [调度模式]                            |
|  ◉ 不启用                            |
|  ○ 间隔执行(-interval)               |
|  ○ 每日定点(-at)                     |
|  ○ Cron 表达式(-cron)                |
|                                      |
| [间隔参数]                            |
|  间隔分钟数  [  60  ] 分钟            |
|                                      |
| [定点参数]                            |
|  执行时间  [6:00,12:00,18:00       ] |
|  逗号分隔，格式 H:mm                  |
|                                      |
| [Cron 参数]                           |
|  Cron 表达式  [0 */6 * * *         ] |
|  分 时 日 月 周                       |
|                                      |
| [时区设置]                            |
|  时区(-tz)  [Local -- 系统默认     v] |
|                                      |
| [调度预览]                            |
|  下次执行  2026-03-16 06:00:00       |
|  再下次    2026-03-16 12:00:00       |
+--------------------------------------+
```

**手机版适配说明**：
- RadioButton 纵向排列，选中模式对应分组展开，其余折叠（display:none）
- 时区 DropdownField 全宽，避免文字截断
- 调度预览标签字体 13dp，颜色 `#e8eaf0`

### 4.5 页面 5 — 测速结果

**对应桌面页**：页面 8 — 测速结果
**无路径字段**

```
+--------------------------------------+
| 测速结果                              |
| ------------------------------------ |
| 扫描IP | 有效IP | 最快延迟 | 最高速度 |
|  1860  |   38   |  48 ms  | 82 Mbps  |
| ------------------------------------ |
| 地区[全部v] 延迟[  0]ms 速[  0]MB/s  |
|                     
                         [应用筛选]        |
| [复制�?名] [复制全部] [导出CSV]      |
| #  IP            延迟  速度  地区     |
| 1  104.19.55.123 48ms  82Mb  HKG     |
| 2  104.18.32.45  52ms  76Mb  NRT     |
+--------------------------------------+
```

**手机版适配说明**：概况卡片两列排布；结果表格去掉抖动列，保留 #/IP/延迟/速度/地区�?
### 4.6 更多抽屉

```
+--------------------------------------+
| �?更多              [x]             |
| [ Hosts 更新  ->  hosts 文件自动配置 ] |
| [ 输出设置    ->  CSV / onlyip 输出  ] |
| [ 其他设置    ->  日志 / 调试选项    ] |
| [ 关于        ->  版本信息 / 仓库    ] |
+--------------------------------------+
```

抽屉关闭方式：点击 [x]、点击抽屉外区域或向下滑动。

### 4.7 Hosts 更新（抽屉入口）

**路径显示**：Hosts 文件路径为只读 Label，固定为系统默认 hosts 路径

```
| Hosts 文件路径                        |
|  /etc/hosts                 [只读]   |
| □ 仅预览不写入(-hosts-dry-run)        |
```

**手机版适配说明**：路径为只读 Label（`field-path-readonly`），由代码运行时赋值，用户不可编辑。

### 4.8 输出设置（抽屉入口）

**路径显示**：CSV 和 onlyip 路径均为只读 Label，固定为 `Application.persistentDataPath` 下文件

```
|  CSV 文件路径(-o)                     |
|  /data/.../result.csv       [只读]   |
|  onlyip 文件路径(-onlyip)             |
|  /data/.../onlyip.txt       [只读]   |
|  尚未生成          [分享文件]        |
```

**手机版适配说明**：路径由 C# 代码在 Awake 阶段写入 Label.text；「打开所在位置」改为「分享文件」。

### 4.9 其他设置（抽屉入口）

无路径字段。日志滚动区 `flex-grow:1` 占满剩余高度；系统集成选项在移动端隐藏。

---

## 五、交互行为说明

### 5.1 开始 / 停止互斥

```
运行中：Start disabled=true，Stop enabled=true，m-progress-area--active 存在
空闲中：Start enabled=true， Stop disabled=true，m-progress-area--active 移除
```

### 5.2 Tab 切换

移除旧 Tab 的 `m-tab-item--active`，添加到新 Tab；同步切换页面的 `m-page--active`。「更多」Tab 触发底部抽屉滑入，不切换页面。

### 5.3 结果角标

`done` 消息到达后：移除 `m-result-badge--hidden`，更新数字，自动跳转结果 Tab。

### 5.4 数据绑定

可编辑控件读写 `CfstOptions`；文件路径 Label 由代码写入 `text` 属性，不参与用户输入绑定。

---

## 六、与桌面版差异对照表

| 特性 | 桌面版 | 手机竖屏版 |
|------|--------|----------|
| 导航结构 | 左侧固定侧边栏 192px | 底部 Tab 6项 + 更多抽屉 |
| 操作按钮 | 左侧栏底部 | 顶部操作栏右侧 |
| 进度条 | 左侧栏底部 | 内容区与 Tab 之间（运行时出现）|
| 表单标签宽 | 120px | 80dp |
| 控件最小高 | 30px | 36dp |
| 文件路径 | 可编辑 TextField + 浏览按钮 | 只读 Label，路径由代码写入 |
| 路径来源 | 用户手动指定 | `Application.persistentDataPath` |
| 结果表格列 | 7 列含抖动 | 5 列去掉抖动 |
| 更多页面 | 直接导航 | 底部抽屉展开 |

---

## 七、路径只读化改动清单

以下控件已从可编辑 `TextField` 改为只读 `Label`（USS 类 `field-path-readonly`）：

| 文件 | 控件 name | 路径来源 |
|------|-----------|----------|
| `PageIpSource.uxml` | `field-ipv4` | `persistentDataPath + "/ip.txt"` |
| `PageIpSource.uxml` | `field-ipv6` | `persistentDataPath + "/ipv6.txt"` |
| `PageHosts.uxml` | `field-hostsfile` | 系统默认 hosts 路径（运行时探测）|
| `PageOutput.uxml` | `field-outputfile` | `persistentDataPath + "/result.csv"` |
| `PageOutput.uxml` | `field-onlyipfile` | `persistentDataPath + "/onlyip.txt"` |

浏览按钮（`btn-browse-*`）已从 UXML 物理删除。USS 样式 `field-path-readonly` 已添加到 `MainWindow.uss`。

---

*文档版本：v1.2 | 最后更新：2026-03-18*
