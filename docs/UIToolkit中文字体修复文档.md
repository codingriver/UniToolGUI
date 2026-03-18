# UIToolkit 系统字体自动回退文档

## 概述

本项目使用运行时动态加载系统字体的方案，无需在项目内内置任何字体文件。
`SystemFontFallback.cs` 组件在游戏启动的 `Awake` 阶段自动从操作系统加载字体，
并注入到 UIToolkit 的 `PanelSettings.textSettings`，实现首帧渲染前字体已就绪。

---

## 项目字体配置文件清单

| 文件 | 作用 | 当前状态 |
|------|------|----------|
| `Assets/Fonts/SystemFontFallback.cs` | 运行时字体加载核心组件 | ✅ 挂载到 CFST GameObject |
| `Assets/Fonts/SystemFontHelper.cs` | 系统字体候选路径工具类（已废弃，路径已合并到 SFF） | ⚠️ 保留兼容 |
| `Assets/Fonts/arial.ttf` | 从系统复制的 Arial 字体（当前未使用） | 📦 闲置 |
| `Assets/UI/MainWindow.uss` | 全局样式表，当前无 `-unity-font` 定义 | ✅ 正常 |
| `Assets/UnityThemes/UnityDefaultRuntimeTheme.tss` | 主题入口，仅含 `unity-theme://default` | ✅ 正常 |
| `Assets/TextSettings.asset` | TextCore 全局设置，DefaultFontAsset 为空 | ✅ 正常 |
| `Assets/Resources/PanelSettings.asset` | UIDocument 面板设置 | ✅ 正常 |

---

## 加载流程详解

```
游戏启动
  └─ Awake() 同步执行（首帧渲染前）
       └─ LoadAndInject()
            ├─ 1. BuildPathGroups()       根据 languagePreset 构建字体候选路径组列表
            ├─ 2. FindFirstExisting()     每组取第一个在磁盘上实际存在的路径
            ├─ 3. new Font(path)          加载系统字体文件（Legacy Font 对象）
            ├─ 4. FontAsset.CreateFontAsset()  创建动态 SDF FontAsset
            │    ├─ 第一组 → _primary（主字体）
            │    └─ 其余组 → _fallbacks（回退字体列表）
            ├─ 5. Inject()               将字体注入所有 UIDocument 的 PanelSettings
            │    ├─ textSettings.defaultFontAsset = primary
            │    └─ 已有 defaultFont 时，将 _primary 插入其 fallbackFontAssetTable[0]
            └─ 6. StartCoroutine(LateInject())  延迟一帧再注入一次，覆盖晚初始化的 UIDocument
```

### 为什么在 Awake 而不是 Start？

Unity 的渲染顺序：`Awake` → `OnEnable` → `Start` → 第一帧渲染。
若在 `Start` 中注入字体，UIDocument 已经完成首次布局，文字会先显示空白再跳变为正确字体。
在 `Awake` 中同步注入，保证首帧渲染时字体已就绪，消除闪烁。

### 为什么还需要 LateInject？

极少数情况下，场景中的 UIDocument 在 `Awake` 时尚未初始化（如从 Prefab 异步实例化）。
`LateInject` 在下一帧补注入一次，作为兜底保障。

---

## Inspector 参数说明

### Language Preset（语言组）

所有选项均自动包含英文字符，无需额外配置。

| 选项 | 适用场景 | Windows 字体 | 加载速度 |
|------|---------|-------------|----------|
| `LatinOnly` | 纯英文界面 | Segoe UI → Arial | ★★★★★ |
| `ChineseSimplified` | 简体中文应用（**推荐**） | 微软雅黑 → 宋体 | ★★★★☆ |
| `ChineseTraditional` | 繁体中文应用 | 微软正黑 → 新细明体 | ★★★★☆ |
| `ChineseBoth` | 简繁同时支持 | 微软雅黑 + 微软正黑 | ★★★☆☆ |
| `Japanese` | 日文应用 | 游ゴシック → メイリオ | ★★★★☆ |
| `Korean` | 韩文应用 | 맑은고딕 → 굴림 | ★★★★☆ |
| `CJKAll` | 全中日韩 | 以上全部 | ★★★☆☆ |
| `Arabic` | 阿拉伯语 | Arial → Tahoma | ★★★★☆ |
| `Thai` | 泰语 | Tahoma → Cordia New | ★★★★☆ |
| `AllLanguages` | 全语言国际化 | 以上全部 | ★★☆☆☆ |

### Atlas Size Preset（初始贴图尺寸）

Dynamic 模式下 Atlas 会按需自动扩展（需开启 MultiAtlas，已默认开启）。
初始尺寸只影响启动时的内存分配，不限制最终可显示的字符数量。

| 选项 | 尺寸 | 显存 | 推荐场景 |
|------|------|------|----------|
| `Tiny_256` | 256×256 | ~0.25MB | 字符极少（如纯数字） |
| `Small_512` | 512×512 | ~1MB | **推荐默认**，大多数应用 |
| `Medium_1024` | 1024×1024 | ~4MB | 字符种类较多 |
| `Large_2048` | 2048×2048 | ~16MB | 需要预加载大量字符 |
| `Huge_4096` | 4096×4096 | ~64MB | 静态字体，通常不需要 |

### Render Mode Preset（渲染模式）

| 选项 | 速度 | 质量 | 推荐场景 |
|------|------|------|----------|
| `SDFAA` | ★★★★★ 最快 | 高（12-36px 无差别） | **推荐默认**，动态 UI 字体 |
| `SDF` | ★★★☆☆ 中等 | 中 | 需要较大缩放比例 |
| `SDF8` | ★★☆☆☆ 较慢 | 较高 | 图标字体 |
| `SDF16` | ★★☆☆☆ 慢 | 高 | 大字号标题 |
| `SDF32` | ★☆☆☆☆ 最慢 | 最高 | 预烘焙静态 Atlas |
| `Bitmap` | ★★★★★ 最快 | 低（缩放有锯齿） | 固定像素大小，无缩放需求 |

> **注意**：`SDFAA` 与 `SDF32` 在 12~36px 的 UI 字号下肉眼无差别，
> 但 `SDFAA` 生成速度约是 `SDF32` 的 3 倍。动态字体始终推荐 `SDFAA`。

### Sampling Point Size（采样点大小）

范围：8 ~ 72，默认 **36**。

字形在 Atlas 中的渲染尺寸。影响字体清晰度和加载速度：

| 值 | 加载速度 | 清晰度 | 推荐场景 |
|----|---------|--------|----------|
| 8~16 | 最快 | 低，大字号模糊 | 极限性能，小字号UI |
| 20~28 | 较快 | 中，常规UI够用 | 追求快速加载 |
| **36** | 适中 | 高，12~48px清晰 | **推荐默认** |
| 48~72 | 较慢 | 很高，适合大字号 | 标题/展示型文字 |

> 回退字体（fallback）自动使用 `max(8, samplingPointSize - 8)`，
> 比主字体稍小，节省加载时间。

### Atlas Padding（字形间距）

范围：2 ~ 16，默认 **6**。

Atlas 中每个字形周围的像素间距。影响 SDF 软化边缘范围：

| 值 | 效果 | 推荐场景 |
|----|------|----------|
| 2~3 | Atlas 利用率高，边缘SDF范围小 | 无描边/发光效果 |
| **6** | 平衡（推荐默认） | 常规 UI |
| 8~12 | 边缘SDF范围大，描边/发光更平滑 | 需要描边或发光特效 |
| 16 | 最大范围，Atlas 利用率低 | 特殊效果 |

> 减小 Padding 可提升 Atlas 空间利用率，同等 Atlas 尺寸下可容纳更多字符。
> 若不需要描边/发光效果，可将 Padding 设为 3~4，进一步提升加载速度。

---

## 推荐配置组合

### 当前项目（CFST Windows 桌面应用）

```
Language:           ChineseSimplified
Atlas Size:         Small_512
Render Mode:        SDFAA
Sampling Point Size: 36
Atlas Padding:      6
```

### 追求最快启动（牺牲部分质量）

```
Language:           ChineseSimplified
Atlas Size:         Small_512
Render Mode:        SDFAA
Sampling Point Size: 24
Atlas Padding:      3
```

### 追求最高质量（大屏/展示场景）

```
Language:            ChineseSimplified
Atlas Size:          Medium_1024
Render Mode:         SDF16
Sampling Point Size: 48
Atlas Padding:       9
```

### 多语言国际化应用

```
Language:            AllLanguages
Atlas Size:          Medium_1024
Render Mode:         SDFAA
Sampling Point Size: 32
Atlas Padding:       6
```

---

## 运行时诊断

在 Inspector 中右键 `SystemFontFallback` 组件 → **Run Diagnostics**，Console 输出：

```
[SFF] Language=ChineseSimplified Atlas=Small_512 Render=SDFAA
IsReady=True Primary=[SysFont]msyh
  Fallback: [SysFont]segoeui
[PanelSettings] default=[SysFont]msyh fallbacks=1
```

## 缺字日志 (Missing Glyph Logging)

- 目的：在运行时检测字体回退链中是否覆盖所有需要的 Unicode 字符；若发现缺失字符，会输出日志，便于定位和修复。
- 日志格式示例：
  - [SFF] Missing glyph U+4E2D for PanelSettings 'MainHUD' in font chain.
- 触发条件：当 enableLog 为 true 时，SystemFontFallback 在注入字体后对 PanelSettings 的字形覆盖进行自检，并对未覆盖的字符输出日志。
- 解决思路：确保系统字体回退链覆盖目标字符。必要时可扩展回退字体（如加入 PathsEmoji、Greek、Cyrillic、Indic 等），或对目标语言增补额外字体路径。

---

## 常见问题

### 中文仍显示空白/方块

1. 确认 `SystemFontFallback` 已挂载到场景 CFST GameObject
2. 开启 `enableLog`，查看 Console 中 `[SFF]` 日志
3. 确认 `[SFF] Primary:` 行有输出（字体加载成功）
4. 若无输出，检查系统是否安装微软雅黑

### 字体加载成功但 UIToolkit 未生效

1. 查看 `[SFF] Set` 或 `[SFF] Prepend` 日志，确认注入到正确的 PanelSettings
2. 确认 PanelSettings 有绑定 textSettings
3. 若 textSettings 为 null，在 PanelSettings Inspector 中手动指定 TextSettings.asset

### 首帧短暂空白

通常不可见（仅一帧）。若明显可见：
**Edit → Project Settings → Script Execution Order**
将 `SystemFontFallback` 设为 -100，使其早于 `MainWindowController` 执行。

### 打包后字体不显示

打包后从目标机器系统字体目录读取。若目标机器未安装微软雅黑，
需在 `PathsCJK_SC` 中增加备用路径，或将字体放入 StreamingAssets。

---

## 技术原理

### 为什么不用 USS `-unity-font`？

`-unity-font: url(...)` 要求字体在项目 Assets 目录内，无法引用系统绝对路径。
`SystemFontFallback` 通过 `FontAsset.CreateFontAsset(new Font(systemPath))` 在运行时
直接从系统路径创建 FontAsset，实现零包体内置、自动适配系统字体。

### 字体回退链结构

```
textSettings.defaultFontAsset = [SysFont]msyh
  └─ fallbackFontAssetTable[0] = [SysFont]segoeui
```

UIToolkit 渲染字符时，先查主字体，找不到则遍历 fallback 列表。

### Dynamic + MultiAtlas 机制

- `AtlasPopulationMode.Dynamic`：按需生成字形，首次渲染某字符时才计算 SDF
- `enableMultiAtlasSupport: true`：当前 Atlas 满时自动创建新 Atlas
- 两者结合：小初始内存 + 无限字符集支持

---

## 自动回退系统字体全流程

以下是从游戏启动到屏幕文字显示的完整链路：

```
[1] 游戏启动，Unity 引擎初始化场景
        ↓
[2] Awake() 执行 — SystemFontFallback
    │
    ├─ BuildPathGroups()
    │    └─ 根据 languagePreset 枚举，按优先级组装候选路径组
    │         例：ChineseSimplified → [PathsCJK_SC, PathsLatin]
    │
    ├─ FindFirstExisting(paths)
    │    └─ 遍历每组候选路径，返回第一个 File.Exists() == true 的路径
    │         Windows 示例：C:\Windows\Fonts\msyh.ttc
    │
    ├─ new Font(path)
    │    └─ Unity Legacy Font API，将系统字体文件映射为内存中的 Font 对象
    │         不加载所有字形，仅建立文件句柄
    │
    ├─ FontAsset.CreateFontAsset(font, ...)
    │    └─ TextCore 引擎创建空的动态 FontAsset
    │         此时 Atlas 贴图已分配（按 AtlasSizePreset），但字形表为空
    │
    ├─ Inject()
    │    ├─ 遍历所有 UIDocument.panelSettings
    │    ├─ 若 textSettings.defaultFontAsset == null
    │    │    └─ 直接设置 defaultFontAsset = _primary
    │    └─ 若已有 defaultFontAsset
    │         └─ 将 _primary 插入 fallbackFontAssetTable[0]（最高优先级）
    │
    └─ StartCoroutine(LateInject())  — 延迟一帧补注入
        ↓
[3] 第一帧渲染开始
    │
    └─ UIToolkit VisualElement 布局计算完成，准备绘制文字
        ↓
[4] TextCore 字形请求
    │
    ├─ 对每个需要渲染的字符 Unicode 码点
    ├─ 查询 defaultFontAsset.characterTable
    │    ├─ 命中 → 直接使用已有 Atlas 坐标绘制
    │    └─ 未命中 → 触发动态字形生成
    │         ├─ 调用 FreeType 光栅化字形
    │         ├─ 计算 SDF（按 RenderMode）
    │         ├─ 写入当前 Atlas 贴图空闲区域
    │         └─ 更新 characterTable 缓存
    │
    └─ 若 Atlas 空间不足 → 触发 MultiAtlas 扩展（见下节）
        ↓
[5] GPU 绘制
    └─ UIToolkit 将字形 UV 坐标映射到 Atlas 贴图，提交 DrawCall
```

---

## Atlas Size 不足时的自动处理机制

### 触发条件

当前 Atlas 贴图中没有足够的连续空闲区域容纳新字形时，触发扩展。

### 处理流程（enableMultiAtlasSupport = true 时）

```
新字形请求
    ↓
TextCore 尝试在当前 Atlas 中找空闲区域
    ↓
找不到足够空间
    ↓
创建新的 Atlas 贴图（尺寸与初始 Atlas 相同）
    ↓
将新字形写入新 Atlas
    ↓
FontAsset.atlasTextures[] 数组追加新贴图
    ↓
后续字形优先填充新 Atlas，旧 Atlas 中已有字形不移动
```

### 各种情况的行为对比

| 设置 | Atlas 满时行为 | 副作用 |
|------|--------------|--------|
| `enableMultiAtlasSupport = true`（当前） | 自动创建新 Atlas，无感知 | 显存随 Atlas 数量线性增长 |
| `enableMultiAtlasSupport = false` | 新字形无法渲染，显示方块 | 无额外显存，但字符受限 |
| `AtlasPopulationMode.Static` | 完全不支持动态添加 | Atlas 固定，性能最优 |

### 实际影响

- **Small_512** 配置下，常规中文 UI（约 500~800 个不重复汉字）通常需要 2~4 个 Atlas
- 每个 512×512 的 Atlas 约占 1MB 显存，4个共 4MB，完全可接受
- Atlas 扩展发生在首次渲染新字符时，会有约 0.1~1ms 的卡顿（单个字形）
- 若界面文字种类固定，扩展只发生一次，后续帧无额外开销

### 建议

- 若界面字符种类多（如长文本列表），建议用 **Medium_1024**，减少 Atlas 分裂次数
- 若对内存极其敏感，用 **Small_512** + MultiAtlas，按需增长
- 不建议禁用 MultiAtlas，否则超出 Atlas 的字符将显示方块

---

## 启动时字符收集 vs Unity 自带流程对比

### 方案一：启动时预收集 Unicode 字符，批量烘焙 Atlas

**原理：** 在启动时扫描所有 UI 文本内容，提取全部 Unicode 字符集，
一次性调用 `FontAsset.TryAddCharacters()` 批量生成字形，填充 Atlas。

```
启动 → 扫描所有 Label/Button 文本 → 收集 Unicode 集合
     → TryAddCharacters(unicodeSet) → 批量 SDF 烘焙
     → Atlas 预热完成 → UI 渲染无动态生成开销
```

**优点：**
- 运行时渲染零延迟，所有字形已预热
- 适合字符集固定的应用（如本项目 CFST）
- Atlas 利用率高，无碎片

**缺点：**
- 启动时有一次集中卡顿（字符越多越久，中文 500 字约 50~200ms）
- 需要手动维护字符收集逻辑
- 若文本内容动态变化（如从网络加载），预收集不完整

### 方案二：Unity TextCore 自带动态流程（当前方案）

**原理：** `AtlasPopulationMode.Dynamic`，每次渲染时按需生成缺失字形。

```
启动 → FontAsset 空 Atlas → UI 渲染
     → 每遇到新字符 → 实时 SDF 烘焙 → 写入 Atlas → 渲染
     → 下次遇到同字符 → 直接从 Atlas 读取，无开销
```

**优点：**
- 启动极快，Atlas 初始为空
- 自动适配任意字符，无需预知字符集
- 内存按需增长，不浪费

**缺点：**
- 首次渲染新字符有约 0.1~1ms 卡顿（通常不可感知）
- 若大量新字符同帧出现（如翻页），可能出现短暂帧率下降
- Atlas 可能因字符出现顺序产生碎片

### 方案对比总结

| 维度 | 预收集批量烘焙 | Unity 自带动态（当前） |
|------|--------------|----------------------|
| 启动速度 | 较慢（集中烘焙） | 快（Atlas 为空） |
| 运行时流畅度 | 极流畅（零动态开销） | 首次新字符有微小卡顿 |
| 内存占用 | 固定（预分配） | 按需增长 |
| 实现复杂度 | 高（需收集逻辑） | 低（Unity 自动处理） |
| 适合场景 | 字符集固定、追求流畅 | 字符集动态、追求快速启动 |
| 本项目推荐 | 可选优化 | ✅ 当前方案，已满足需求 |

### 如何为本项目添加预收集优化（可选）

若需要更流畅的首次渲染体验，可在 `SystemFontFallback` 的 `Inject()` 之后添加：

```csharp
// 收集场景中所有文本字符并预热 Atlas
private void PrewarmAtlas(FontAsset fa)
{
    var chars = new System.Collections.Generic.HashSet<uint>();
    foreach (var label in FindObjectsOfType<UnityEngine.UIElements.Label>())
        foreach (char c in label.text) chars.Add(c);
    uint[] arr = new uint[chars.Count];
    chars.CopyTo(arr);
    fa.TryAddCharacters(arr);
    Log("[SFF] Prewarmed " + arr.Length + " chars");
}
```

调用时机：在 `Inject()` 之后、`LateInject` 协程中调用，避免阻塞 Awake。

> 本项目 CFST 界面文字以固定中文标签为主，字符集稳定，
> 当前 Dynamic 方案已完全满足需求，无需额外实现预收集逻辑。

---

## 大量字符与全球化场景下的字体尺寸控制方案

### 问题分析

当应用需要支持大量中文字符（如全文搜索结果、日志输出）或同时支持多语言（全球化频道），
Atlas 尺寸会快速增长，带来以下问题：

| 问题 | 原因 | 影响 |
|------|------|------|
| 显存持续增长 | MultiAtlas 不断创建新贴图 | 内存压力，低端设备崩溃 |
| DrawCall 增加 | 每个 Atlas 贴图产生独立 DrawCall | 渲染性能下降 |
| Atlas 碎片化 | 字符随机出现，Atlas 空间不连续 | 同等显存容纳字符数减少 |
| 无用字符占用 | 曾显示过的字符永久留在 Atlas | 浪费空间 |

### 核心控制参数对比

| 参数 | 影响 | 节省空间的设置 |
|------|------|---------------|
| `samplingPointSize` | 每个字形在 Atlas 中的像素面积 | 24 比 36 节省约 56% 空间 |
| `atlasPadding` | 字形间距 | 3 比 6 节省约 20% 空间 |
| `atlasWidth/Height` | 单张 Atlas 容量 | 按需选择，不宜过大 |
| `enableMultiAtlasSupport` | 是否允许无限扩展 | 关闭可硬限制，但字符超出会显示方块 |

### 方案一：分级字体策略（推荐全球化场景）

不同语言使用不同的 `samplingPointSize`，按使用频率分配质量资源：

```
主界面中文（高频）：samplingPointSize=36, Atlas=1024, SDFAA
日志/输出文本（中频）：samplingPointSize=24, Atlas=512, SDFAA
阿拉伯/泰语（低频）：samplingPointSize=20, Atlas=256, SDFAA
```

实现方式：在 `SystemFontFallback` 中为不同用途创建多个 FontAsset，
通过 USS class 指定不同控件使用不同字体：

```css
/* 主界面标签 - 高质量 */
.label-main { -unity-font-definition: ...; font-size: 14px; }

/* 日志输出 - 轻量 */
.label-log  { -unity-font-definition: ...; font-size: 12px; }
```

### 方案二：Atlas 尺寸硬上限控制

当需要严格控制显存时，关闭 MultiAtlas 并设置合适的初始尺寸：

```csharp
// 关闭 MultiAtlas，Atlas 满后新字符显示方块（而非无限扩展）
fa = FontAsset.CreateFontAsset(font,
    samplingPointSize: 24,
    atlasPadding: 3,
    renderMode: GlyphRenderMode.SDFAA,
    atlasWidth: 1024, atlasHeight: 1024,
    atlasPopulationMode: AtlasPopulationMode.Dynamic,
    enableMultiAtlasSupport: false);  // 硬限制
```

**容量估算公式：**

```
单张 Atlas 可容纳字形数 ≈ (atlasSize / (samplingPointSize + padding*2))^2

示例：
  1024px Atlas, samplingPointSize=24, padding=3
  → (1024 / (24+6))^2 ≈ 1170 个字形

  1024px Atlas, samplingPointSize=36, padding=6
  → (1024 / (36+12))^2 ≈ 455 个字形
```

### 方案三：动态字体分页（大文本列表）

对于日志、搜索结果等大量动态文本，不应将所有字符加载到同一 FontAsset。
推荐使用虚拟化列表（只渲染可视区域的条目），限制同屏字符数量：

```
可视区域 10 行 × 每行 50 字符 = 500 字符/帧
 → 远小于 1024px Atlas 的容量上限
 → 无需担心 Atlas 溢出
```

UIToolkit 的 `ListView` 组件内置虚拟化支持，启用方式：

```csharp
var listView = root.Q<ListView>("log-list");
listView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
listView.fixedItemHeight = 24;
```

---

## Atlas 字符清理机制

### Unity TextCore 的默认行为：字符永不移除

**这是 Unity TextCore 的设计决策**：
一旦字形被写入 Atlas，永远不会被自动移除，即使该字符不再显示。

原因：
- 移除字形需要重新打包整个 Atlas（CPU 密集操作）
- 字形的 UV 坐标被 Mesh 缓存，移除后需要重建所有使用该字形的 Mesh
- 动态字体的设计目标是「增量增长」，不是「精确管理」

### 何时需要主动清理

以下场景需要考虑主动清理 Atlas：

| 场景 | 原因 |
|------|------|
| 多语言切换（如从中文切换到阿拉伯语） | 旧语言字形占用大量 Atlas 空间 |
| 临时显示大量文本（如搜索结果）后返回主界面 | 临时字符永久占用 Atlas |
| 长时间运行的应用（如 24h 运行的工具软件） | Atlas 持续增长，最终耗尽显存 |

### 主动清理方案

#### 方案 A：重建 FontAsset（彻底清理）

```csharp
/// 完全重建 FontAsset，清除所有字形缓存
/// 适合场景切换、语言切换时调用
public void RebuildFontAsset()
{
    // 销毁旧 FontAsset
    if (_primary != null)
    {
        // 断开注入
        foreach (var doc in FindObjectsOfType<UIDocument>())
        {
            var ts = doc.panelSettings?.textSettings;
            if (ts != null && ts.defaultFontAsset == _primary)
                ts.defaultFontAsset = null;
        }
        Destroy(_primary);
        _primary = null;
    }
    foreach (var fb in _fallbacks) if (fb != null) Destroy(fb);
    _fallbacks.Clear();

    // 重新加载
    LoadAndInject();
    Log("[SFF] FontAsset rebuilt and Atlas cleared");
}
```

**代价：** 重建后首帧所有字符重新触发动态生成，有短暂卡顿。

#### 方案 B：ClearFontAssetData（部分清理）

```csharp
/// 清除字形数据但保留 FontAsset 对象
/// Unity 2021.2+ 支持
public void ClearAtlas()
{
    if (_primary == null) return;
    // 清除字符表和 Atlas 贴图内容
    _primary.ClearFontAssetData(setAtlasSizeToZero: false);
    Log("[SFF] Atlas cleared, glyphs will regenerate on next render");
}
```

**注意：** `ClearFontAssetData` 会清除所有已缓存字形，下次渲染时重新生成。

#### 方案 C：Atlas 使用率监控 + 阈值触发

```csharp
/// 监控 Atlas 数量，超过阈值时自动重建
private void Update()
{
    if (_primary == null) return;
    int atlasCount = _primary.atlasTextures?.Length ?? 0;
    if (atlasCount > maxAtlasCount)  // maxAtlasCount 建议设为 4
    {
        Log("[SFF] Atlas count " + atlasCount + " exceeded limit, rebuilding...");
        RebuildFontAsset();
    }
}
```

### 各清理方案对比

| 方案 | 触发时机 | 清理彻底度 | 重建开销 | 适用场景 |
|------|---------|------------|---------|----------|
| 重建 FontAsset | 手动/场景切换 | 完全清零 | 高（首帧卡顿） | 语言切换、场景切换 |
| ClearFontAssetData | 手动/定期 | 清零字形表 | 中 | 定期维护 |
| Atlas 数量阈值 | 自动监控 | 完全清零 | 高 | 长时间运行应用 |
| 不清理（默认） | 无 | 无 | 无 | 字符集固定的应用 |

### 本项目建议

CFST 是桌面工具软件，界面文字固定，字符集不超过 1000 个汉字，
**无需实现 Atlas 清理逻辑**。当前 Dynamic + MultiAtlas 方案在整个运行周期内
Atlas 数量不会超过 4 个（约 4MB 显存），完全可接受。

如果未来增加日志全文显示或搜索功能，建议：
1. 日志控件使用独立的轻量 FontAsset（`samplingPointSize=20`）
2. 启用 `ListView` 虚拟化，限制同屏字符数
3. 在「清空日志」操作时调用 `ClearFontAssetData` 回收 Atlas 空间
        
