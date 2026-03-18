# Unity UIToolkit 使用注意事项

本文档总结在 Unity 2022 UIToolkit 开发中实际遇到的问题与解决方案，供后续开发参考。

---

## 一、字体与中文显示

### 1.1 UIToolkit 字体系统独立于 TextMeshPro

UIToolkit 与 TextMeshPro 使用**完全独立**的字体资产体系。
项目中已有的 `.asset` TMP 字体文件**不能直接用于 UIToolkit**，必须单独生成或配置。

### 1.2 两种字体引用方式

| USS 属性 | 说明 | 推荐程度 |
|---|---|---|
| `-unity-font` | 引用原始 TTF/OTF/TTC，运行时动态生成字形 | ⚠️ 不推荐 |
| `-unity-font-definition` | 引用预编译 FontAsset（.asset），静态 Atlas 贴图 | ✅ 推荐 |

**优先使用 `-unity-font-definition`**，原因：
- 运行时无需 CPU 实时生成字形，性能更好
- CJK 字符显示更稳定，不会出现方块或乱码
- WebGL、移动端兼容性更好
- Unity 2022 对动态 CJK 字形生成支持不稳定

### 1.3 字体文件路径大小写敏感

USS 中的路径**区分大小写**，必须与文件名完全一致：

```css
/* ❌ 错误 */
-unity-font-definition: url("/Assets/Fonts/SourceHanSansSC-bold UIToolkit.asset");

/* ✅ 正确 */
-unity-font-definition: url("/Assets/Fonts/SourceHanSansSC-Bold UIToolkit.asset");
```

### 1.4 全局字体的正确配置链路

正确的继承链路为：

```
PanelSettings.asset
  └── Theme: UnityDefaultRuntimeTheme.tss
        └── @import GlobalFont.uss
              └── -unity-font-definition: SourceHanSansSC-Bold UIToolkit.asset
```

子页面（Pages/*.uxml）通过 PanelSettings 主题链自动继承，**无需在每个 UXML 中单独引用字体**。
只有 MainWindow.uxml 等顶层 UXML 需要显式引用 USS（`<Style src="..."/>`）。

### 1.5 FontAsset 生成参数

使用 **Window → TextMeshPro → Font Asset Creator** 生成，关键参数：

| 参数 | 推荐值 |
|---|---|
| Atlas Resolution | 4096 × 4096 |
| Render Mode | SDFAA |
| Character Set | Custom Range |
| Custom Range | `32-126,19968-40959,65281-65374` |

- `32-126`：ASCII 基本字符
- `19968-40959`：CJK 基本汉字（约 2 万个）
- `65281-65374`：全角字符

### 1.6 运行时字符显示为方块的排查

1. 检查字符 Unicode 是否在生成 Atlas 时的范围内
2. 检查 PanelSettings 的 Theme Style Sheet 是否正确指向 `UnityDefaultRuntimeTheme`
3. 确认 FontAsset 已放入 `Assets/Resources/` 目录（保证 Build 时被打包）
4. 执行 **Assets → Refresh**（Ctrl+R）后重新进入 Play Mode 测试

---

## 二、样式表（USS）注意事项

### 2.1 USS 不支持所有 CSS 特性

USS 是 CSS 的子集，以下常见 CSS 特性**不支持**：

- `calc()` 表达式（Unity 2022 不支持）
- CSS 变量 `var(--xxx)`（Unity 2022 不支持）
- 伪类选择器仅支持 `:hover`、`:active`、`:focus`、`:checked`、`:disabled`
- 不支持 CSS Grid，仅支持 Flex 布局

### 2.2 长度单位

| 单位 | 含义 |
|---|---|
| `px` | 像素（固定值）|
| `%` | 相对父容器 |
| `vw` / `vh` | 视口宽高（Unity 2022 支持有限）|

**不支持** `em`、`rem`、`fr` 等单位。

### 2.3 USS 中 url() 路径规则

- 以 `/` 开头为**项目根目录**绝对路径（从 Assets/ 开始）
- 不以 `/` 开头为**相对于当前 USS 文件**的相对路径
- 推荐使用绝对路径，避免移动文件后路径失效

```css
/* 绝对路径（推荐） */
-unity-font-definition: url("/Assets/Fonts/MyFont.asset");

/* 相对路径 */
-unity-font-definition: url("../Fonts/MyFont.asset");
```

### 2.4 主题继承顺序

USS 样式优先级（从低到高）：

1. 主题 TSS（全局默认）
2. UXML 中通过 `<Style>` 引入的 USS
3. 元素的 `style` 属性（内联样式）
4. 代码中通过 `element.style.xxx` 设置的样式

---

## 三、UXML 结构注意事项

### 3.1 xmlns 命名空间声明

所有 UXML 文件必须包含正确的命名空间声明，否则自定义控件无法识别：

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements"
         xmlns:uie="UnityEditor.UIElements">
```

### 3.2 name 与 class 的区别

| 属性 | USS 选择器 | 用途 |
|---|---|---|
| `name="myLabel"` | `#myLabel` | 唯一标识，代码查询用 |
| `class="my-class"` | `.my-class` | 样式分类，可多个 |

代码中查询元素：
```csharp
// 按 name 查询（推荐用于唯一元素）
var label = root.Q<Label>("myLabel");

// 按 class 查询（返回第一个匹配）
var btn = root.Q<Button>(null, "primary-btn");

// 查询所有匹配
var allBtns = root.Query<Button>(null, "primary-btn").ToList();
```

### 3.3 VisualElement 层级与性能

- 避免嵌套层级过深（建议不超过 8 层），深层嵌套会增加布局计算开销
- 动态增删大量子元素时，使用 `Add()`/`Remove()` 而非每帧重建整个树
- 频繁更新的数据（如进度、数字）优先用 `label.text = ...` 而非重建元素

---

## 四、C# 脚本与 UIToolkit 交互

### 4.1 必须在主线程操作 UI

UIToolkit 的所有 UI 操作**必须在 Unity 主线程执行**。
在异步方法、`Task`、线程池回调中更新 UI，必须通过调度器切回主线程：

```csharp
// ❌ 错误：在非主线程直接操作 UI
Task.Run(() =>
{
    myLabel.text = "done"; // 可能崩溃或静默失败
});

// ✅ 正确：通过 UnityMainThreadDispatcher 切回主线程
Task.Run(() =>
{
    var result = DoHeavyWork();
    UnityMainThreadDispatcher.Instance.Enqueue(() =>
    {
        myLabel.text = result;
    });
});
```

### 4.2 RegisterCallback 与 UnregisterCallback

事件注册后**必须在适当时机注销**，否则会造成内存泄漏（尤其在动态创建/销毁的面板中）：

```csharp
// 注册
btn.RegisterCallback<ClickEvent>(OnClick);

// 销毁时注销
void OnDisable()
{
    btn.UnregisterCallback<ClickEvent>(OnClick);
}
```

### 4.3 schedule 用于延迟/轮询

在 UIToolkit 中执行延迟操作或轮询，使用 `schedule` 而非 `Invoke` 或 `Coroutine`：

```csharp
// 延迟 500ms 执行
element.schedule.Execute(() =>
{
    label.text = "delayed";
}).StartingIn(500);

// 每 1000ms 轮询一次
element.schedule.Execute(() =>
{
    UpdateStatus();
}).Every(1000);
```

### 4.4 避免在构造函数中查询子元素

在 `MonoBehaviour.Awake()` 或自定义控件构造函数中，UXML 树可能尚未绑定完成。
应在 `Start()` 或 `CreateGUI()` 中执行元素查询：

```csharp
// ❌ 在 Awake 中查询可能得到 null
void Awake() { myLabel = root.Q<Label>("title"); }

// ✅ 在 Start / CreateGUI 中查询
void Start() { myLabel = root.Q<Label>("title"); }
```

---

## 五、与 .NET Standard 2.1 / Unity C# 版本的兼容性

Unity 2022 默认使用 **.NET Standard 2.1**，对应 **C# 9.0**，以下新版 C# 语法**不可用**：

| 语法 | 要求版本 | 替代方案 |
|---|---|---|
| 文件级命名空间 `namespace Foo;` | C# 10 | 使用 `namespace Foo { }` 块语法 |
| 集合表达式 `[1, 2, 3]` | C# 12 | 使用 `new int[] { 1, 2, 3 }` |
| `record` 类型 | C# 9（部分支持）| 普通 `class` 替代 |
| 索引从末尾 `^1` | C# 8（需验证）| 使用 `list[list.Count - 1]` |
| `IReadOnlySet<T>` | .NET 5+ | 使用 `IReadOnlyCollection<T>` 或 `HashSet<T>` |
| `Microsoft.Win32.Registry` | 需额外引用 | 改用 P/Invoke `advapi32.dll` |

**最佳实践**：
- 引用第三方代码时，确认其目标框架与 Unity 兼容
- 不要依赖 `Microsoft.Win32.Registry` 程序集，改用 Win32 P/Invoke 直接操作注册表
- 不要使用文件级命名空间，统一使用块级命名空间

---

## 六、PanelSettings 配置要点

| 字段 | 推荐值 | 说明 |
|---|---|---|
| Theme Style Sheet | `UnityDefaultRuntimeTheme.tss` | 全局主题入口，必须正确设置 |
| Scale Mode | `Scale With Screen Size` | 适配不同分辨率 |
| Reference Resolution | `1920 × 1080` | 参考分辨率，按项目实际设置 |
| Sort Order | `0` | 多 PanelSettings 时控制层叠顺序 |

**注意**：一个场景中可以有多个 PanelSettings，但每个 UIDocument 只能绑定一个。
全局字体、主题修改只需修改共享的 PanelSettings，无需逐一修改每个 UIDocument。

---

## 七、调试技巧

### 7.1 UI Debugger

菜单 **Window → UI Toolkit → Debugger** 打开 UI 调试器：
- 可实时查看元素树、已应用的 USS 样式
- 支持鼠标悬停高亮对应元素
- 可查看最终计算后的 Resolved Style（实际生效值）

### 7.2 样式不生效的排查顺序

1. 打开 UI Debugger，选中目标元素，查看 **Matching Selectors** 面板
2. 确认选择器拼写是否正确（`#name` vs `.class`）
3. 检查是否被更高优先级的内联样式覆盖
4. 确认 USS 文件已被正确 `@import` 或通过 `<Style>` 引入
5. 执行 **Assets → Refresh** 让 Unity 重新编译 USS

### 7.3 字体问题快速定位

```
字符显示为方块  →  FontAsset 中不含该字符的 Unicode 码点，重新生成 Atlas
字符显示为空白  →  字体路径错误或 FontAsset 未加载
编辑器正常/运行时乱码  →  FontAsset 未放入 Resources 目录，Build 时未打包
全部文字消失  →  PanelSettings 的 Theme Style Sheet 未设置或路径失效
```

---

## 八、常见错误速查

| 错误现象 | 原因 | 解决方案 |
|---|---|---|
| 中文显示方块 | FontAsset 不含该汉字 | 重新生成 Atlas，扩大 Custom Range |
| 样式完全不生效 | USS 未被引入主题链 | 检查 TSS `@import` 或 UXML `<Style>` |
| `Q<T>()` 返回 null | name/class 拼写错误，或查询时机过早 | 检查拼写，移到 `Start()` 中执行 |
| UI 在子线程更新崩溃 | UIToolkit 非线程安全 | 使用 `UnityMainThreadDispatcher` |
| 字体路径报错 | 路径大小写不匹配 | 严格对照文件名大小写 |
| Build 后字体丢失 | FontAsset 不在 Resources 目录 | 移动到 `Assets/Resources/` 下 |
| `namespace` 编译报错 | 使用了 C# 10 文件级命名空间语法 | 改为 `namespace Foo { }` 块语法 |
| `Registry` 类找不到 | .NET Standard 2.1 不含该程序集 | 改用 Win32 P/Invoke `advapi32.dll` |
| 集合表达式 `[...]` 报错 | C# 12 语法，Unity 不支持 | 改用 `new T[] { ... }` |

---

## 九、推荐目录结构

```
Assets/
├── Fonts/
│   └── SourceHanSansSC-Bold UIToolkit.asset   ← UIToolkit 专用 FontAsset
├── GlobalStyles/
│   └── GlobalFont.uss                          ← 全局字体样式
├── Resources/
│   ├── PanelSettings.asset                     ← UI 面板配置
│   └── Fonts/                                  ← Build 时需要打包的字体副本
├── UI/
│   ├── MainWindow.uxml
│   └── Pages/
│       └── *.uxml
└── UnityThemes/
    └── UnityDefaultRuntimeTheme.tss            ← 主题入口
```

---

---

## 十、ScrollView / Scrollbar 样式注意事项

### 10.1 Scrollbar 子元素使用 class 名而非 ID

Unity 2022 UIToolkit 的 ScrollView 内部滚动条元素在 USS 中**必须用 class 名选择器**，
ID 选择器（`#unity-vertical-scroller` 等）**无法匹配**到实际元素。

| 作用位置 | 正确选择器（class） | 错误写法（ID，不生效） |
|---|---|---|
| 垂直滚动条容器 | `.unity-scroller--vertical` | `#unity-vertical-scroller` |
| 轨道背景 | `.unity-base-slider__tracker` | `#unity-tracker` |
| 滑块 | `.unity-base-slider__dragger` | `#unity-dragger` |
| 上方箭头按钮 | `.unity-scroller__low-button` | `#unity-low-button` |
| 下方箭头按钮 | `.unity-scroller__high-button` | `#unity-high-button` |

### 10.2 必须重置 background-image

Unity 默认主题对 scrollbar 使用**纹理图片**覆盖背景色，
自定义颜色时必须同时设置 `background-image: none`，否则颜色不生效：

```css
/* ❌ 仅设置颜色，被默认纹理覆盖，不生效 */
.unity-scroller--vertical {
    background-color: #161b24;
}

/* ✅ 同时重置纹理，颜色才能生效 */
.unity-scroller--vertical {
    background-color: #161b24;
    background-image: none;
}
```

### 10.3 选择器特异性要高于默认主题

使用**双 class 选择器**（父类 + 目标类）来确保优先级高于默认主题的单 class 规则：

```css
/* 特异性低，可能被默认主题覆盖 */
.unity-scroller--vertical .unity-base-slider__dragger {
    background-color: #303a50;
}

/* 特异性更高，可靠覆盖默认主题 */
.unity-scroll-view .unity-scroller--vertical .unity-base-slider__dragger {
    background-color: #303a50;
}
```

### 10.4 ScrollView 默认不显示滚动条

`ScrollView` 的 `mode="Vertical"` 默认滚动条可见性为 `Auto`，
**内容高度不超过容器时滚动条不渲染**，USS 样式自然无从生效。

调试或强制显示时，在 UXML 中加 `vertical-scroller-visibility="AlwaysVisible"`：

```xml
<!-- 内容不足时滚动条不显示（默认） -->
<ui:ScrollView mode="Vertical">

<!-- 强制始终显示滚动条 -->
<ui:ScrollView mode="Vertical" vertical-scroller-visibility="AlwaysVisible">
```

### 10.5 直接子选择器 `>` 无法穿透 ScrollView 内部层级

ScrollView 的滚动条元素在 shadow DOM 内部，层级与预想不同，
`>` 直接子选择器会匹配失败，必须使用**后代选择器**：

```css
/* ❌ 直接子选择器，匹配失败 */
.nav-scroll > #unity-vertical-scroller > #unity-slider > #unity-dragger {
    background-color: #00cc00;
}

/* ✅ 后代选择器，正确匹配 */
.unity-scroll-view .unity-scroller--vertical .unity-base-slider__dragger {
    background-color: #303a50;
}
```

### 10.6 隐藏上下箭头按钮的正确方式

使用 `display: none` 彻底隐藏，不占布局空间，轨道自动填满全高：

```css
.unity-scroll-view .unity-scroller--vertical .unity-scroller__low-button {
    display: none;
}
.unity-scroll-view .unity-scroller--vertical .unity-scroller__high-button {
    display: none;
}
```

> 不要用 `height: 0` / `min-height: 0` 隐藏按钮，会导致两端视觉异常。

### 10.7 完整的深色主题 Scrollbar 样式模板

```css
/* vertical scroller container */
.unity-scroll-view .unity-scroller--vertical {
    background-color: #161b24;
    background-image: none;
    width: 10px;
    border-left-width: 1px;
    border-left-color: #252d3d;
}

/* tracker */
.unity-scroll-view .unity-scroller--vertical .unity-base-slider__tracker {
    background-color: #161b24;
    background-image: none;
    border-width: 0;
}

/* dragger (thumb) */
.unity-scroll-view .unity-scroller--vertical .unity-base-slider__dragger {
    background-color: #303a50;
    background-image: none;
    border-width: 0;
    border-radius: 3px;
    min-height: 24px;
}
.unity-scroll-view .unity-scroller--vertical .unity-base-slider__dragger:hover {
    background-color: #f48120;
}

/* hide arrow buttons */
.unity-scroll-view .unity-scroller--vertical .unity-scroller__low-button,
.unity-scroll-view .unity-scroller--vertical .unity-scroller__high-button {
    display: none;
}
```

---

### 10.8 实战案例：从 Unity 自动生成到最终正确写法的完整过程

> 本节面向完全不了解 CSS/USS 的开发者，通过一个真实案例演示
> 「Unity 自动生成了什么」→「哪里有问题」→「如何一步步改对」。

---

#### 背景

项目有一个左侧导航栏，用 `ScrollView` 包裹菜单按钮。
需要将滚动条改成深色主题风格：轨道深色、滑块灰蓝色、鼠标悬停变橙色、隐藏上下箭头。

---

#### 第一步：Unity 自动生成的 UXML（原始状态）

Unity UI Builder 新建 ScrollView 时，自动生成的 UXML 如下：

```xml
<!-- Unity 自动生成，没有 name、class、可见性设置 -->
<ui:ScrollView />
```

或者稍微完整一点的版本：

```xml
<ui:ScrollView mode="Vertical">
    <ui:Label text="菜单项 1" />
    <ui:Label text="菜单项 2" />
</ui:ScrollView>
```

**这个自动生成版本有两个问题：**
1. 没有 `class` 属性 → USS 无法通过 `.nav-scroll` 选择器定位它
2. 没有 `vertical-scroller-visibility="AlwaysVisible"` → 内容不够多时滚动条不渲染，样式无从生效

---

#### 第二步：修改 UXML，加上 class 和强制显示

```xml
<!-- 修改后：加了 name、class、强制显示滚动条 -->
<ui:ScrollView
    name="nav-scroll"
    class="nav-scroll"
    mode="Vertical"
    vertical-scroller-visibility="AlwaysVisible">

    <ui:Label text="菜单项 1" />
    <ui:Label text="菜单项 2" />
</ui:ScrollView>
```

> **说明：**
> - `name="nav-scroll"` → C# 代码用 `root.Q("nav-scroll")` 查找这个元素
> - `class="nav-scroll"` → USS 用 `.nav-scroll { }` 给它加样式
> - `vertical-scroller-visibility="AlwaysVisible"` → 强制渲染滚动条，调试期间必须加

---

#### 第三步：USS 错误写法（Unity 文档误导，实际不生效）

很多人（包括参考旧文档）会这样写 USS，**但完全不生效**：

```css
/* ❌ 错误写法一：用 ID 选择器（#），匹配不到任何元素 */
.nav-scroll #unity-vertical-scroller {
    background-color: #161b24;
}
.nav-scroll #unity-vertical-scroller #unity-dragger {
    background-color: #303a50;
}

/* ❌ 错误写法二：用 > 直接子选择器，层级不对 */
.nav-scroll > #unity-vertical-scroller > #unity-slider > #unity-dragger {
    background-color: #303a50;
}

/* ❌ 错误写法三：设了颜色但忘了重置纹理，被默认纹理图片覆盖 */
.unity-scroller--vertical {
    background-color: #161b24;
    /* 缺少 background-image: none → 颜色被图片覆盖，看不出效果 */
}
```

**为什么不生效？**
- Unity 2022 UIToolkit 的 ScrollView 内部元素在渲染时注册的是 **class 名**（如 `.unity-scroller--vertical`），而不是 `#id`
- `>` 直接子选择器要求层级完全匹配，但实际层级中间还有 `#unity-drag-container` 等隐藏层
- 默认主题给滚动条设置了纹理图片，不重置的话颜色被图片盖住

---

#### 第四步：正确写法（最终生效版本）

```css
/* ✅ 正确写法：class 选择器 + background-image 重置 + 足够的特异性 */

/* 1. 整个垂直滚动条区域（包括轨道+按钮的外框） */
.unity-scroll-view .unity-scroller--vertical {
    background-color: #161b24;   /* 深色背景，与侧边栏融合 */
    background-image: none;      /* 必须！清除默认纹理图片 */
    width: 10px;                 /* 滚动条整体宽度 */
    border-left-width: 1px;      /* 左侧加一条分隔线 */
    border-left-color: #252d3d;  /* 分隔线颜色 */
}

/* 2. 轨道（滑块滑动的背景区域） */
.unity-scroll-view .unity-scroller--vertical .unity-base-slider__tracker {
    background-color: #161b24;   /* 与容器同色，视觉上无缝 */
    background-image: none;
    border-width: 0;             /* 去掉轨道边框 */
}

/* 3. 滑块（可拖动的那个小方块） */
.unity-scroll-view .unity-scroller--vertical .unity-base-slider__dragger {
    background-color: #303a50;   /* 灰蓝色，低调但可见 */
    background-image: none;
    border-width: 0;
    border-radius: 3px;          /* 圆角，更现代 */
    min-height: 24px;            /* 滑块最小高度，内容很多时不会太小 */
}

/* 4. 滑块悬停状态（鼠标移上去变色） */
.unity-scroll-view .unity-scroller--vertical .unity-base-slider__dragger:hover {
    background-color: #f48120;   /* 橙色强调色，与导航激活态一致 */
}

/* 5. 隐藏上下箭头按钮（现代 UI 风格，不需要箭头） */
.unity-scroll-view .unity-scroller--vertical .unity-scroller__low-button,
.unity-scroll-view .unity-scroller--vertical .unity-scroller__high-button {
    display: none;               /* 彻底隐藏，不占布局空间 */
}
```

---

#### 第五步：理解选择器写法（USS 基础）

USS 选择器与 CSS 完全相同语法，以下是本案例用到的规则：

```
选择器写法                    含义
─────────────────────────────────────────────────────────
.class-name { }              选中所有拥有该 class 的元素
#element-name { }            选中 name="element-name" 的元素（ID选择器）
.parent .child { }           选中 parent 内部任意层级的 child（后代选择器）
.parent > .child { }         选中 parent 直接子元素中的 child（直接子选择器）
.element:hover { }           鼠标悬停时应用的样式（伪类）
.classA, .classB { }         同时选中 classA 和 classB（多选，逗号分隔）
```

**本案例为什么用后代选择器（空格）而不是直接子选择器（`>`）？**

因为 ScrollView 内部结构如下（简化）：

```
ScrollView (.unity-scroll-view)
└── Scroller (.unity-scroller--vertical)
    ├── Button (.unity-scroller__low-button)    ← 上箭头
    ├── Slider
    │   ├── 轨道 (.unity-base-slider__tracker)
    │   └── 拖拽容器
    │       └── 滑块 (.unity-base-slider__dragger)  ← 中间隔了一层！
    └── Button (.unity-scroller__high-button)   ← 下箭头
```

滑块（`.unity-base-slider__dragger`）在 `Scroller` 和它之间**隔了两层**，
所以必须用后代选择器（空格）而不是直接子选择器（`>`）。

---

#### 第六步：验证是否生效

1. 保存 USS 文件
2. Unity Editor 按 `Ctrl+R` 刷新
3. 运行游戏（Play Mode）
4. 如果看不到滚动条：检查 UXML 是否加了 `vertical-scroller-visibility="AlwaysVisible"`
5. 如果颜色还是没变：打开 **Window → UI Toolkit → Debugger**，
   点击滚动条区域，查看右侧 **Matching Selectors** 面板，
   确认你的选择器出现在列表中，且没有被更高优先级的规则覆盖

---

#### 快速对照表：自动生成 vs 正确写法

| 内容 | Unity 自动生成 | 正确写法 |
|---|---|---|
| UXML ScrollView | `<ui:ScrollView />` | 加 `class` + `vertical-scroller-visibility` |
| USS 选择器 | `#unity-vertical-scroller` | `.unity-scroller--vertical` |
| USS 层级 | `> #unity-slider > #unity-dragger` | ` .unity-base-slider__dragger`（空格后代） |
| USS 颜色 | 只写 `background-color` | 同时加 `background-image: none` |
| 箭头按钮隐藏 | `height: 0` 或 `min-height: 0` | `display: none` |

---


---

## 十一、Button 点击事件注意事项

### 11.1 Button.clicked 与 RegisterCallback\<ClickEvent\> 的区别

UIToolkit 的 `Button` 控件内部通过 `Clickable` manipulator 处理点击，并在内部调用 `StopImmediatePropagation()`。这导致通过 `RegisterCallback<ClickEvent>` 在 **BubbleUp（冒泡）阶段**注册的回调**收不到事件**。

| 方式 | 阶段 | 结果 |
|---|---|---|
| `button.RegisterCallback<ClickEvent>(cb)` | BubbleUp（默认） | ❌ 被 Button 内部拦截，回调不触发 |
| `button.RegisterCallback<ClickEvent>(cb, TrickleDown.TrickleDown)` | TrickleDown（捕获） | ✅ 在 Button 处理前触发 |
| `button.clicked += cb` | Button 专属事件 | ✅ 官方推荐 |

**推荐做法：**

```csharp
// ✅ 方式一：官方推荐
button.clicked += OnButtonClick;

// ✅ 方式二：需要事件对象时用 TrickleDown
button.RegisterCallback<ClickEvent>(evt =>
{
    NavigateTo(idx);
    evt.StopPropagation();
}, TrickleDown.TrickleDown);

// ❌ 错误：BubbleUp 阶段被 Button 内部拦截，永远不触发
button.RegisterCallback<ClickEvent>(evt => Debug.Log("never called"));
```

### 11.2 ScrollView 内的 Button 点击问题

当 `Button` 位于 `ScrollView` 内部且**内容超出容器高度**（即 ScrollView 可滚动状态）时：

- `ScrollView` 会在 `PointerDownEvent` 阶段注册**拖拽检测**，消耗指针事件
- `Button.clicked` 和默认 `ClickEvent` 均无法触发
- **即使 Hover 效果正常，Click 也可能完全失效**

**根本原因：**

```
内容高度 > 容器高度 → ScrollView 进入可滚动状态
         → PointerDown 被拖拽检测消耗
         → Button 收不到完整的 PointerDown+PointerUp 序列
         → clicked / ClickEvent 均不触发
```

**排查方法：**

1. 鼠标 Hover 有变色但点击无响应 → 高度怀疑 ScrollView 拦截
2. 通过 LayoutDump 检查 ScrollView 的 `size` 与内容 `size` 的大小关系
3. 若内容高度 > 容器高度 → ScrollView 可滚动 → 拦截指针事件

**解决方案：使用 TrickleDown 阶段**

```csharp
// ✅ TrickleDown 在 ScrollView 拖拽检测之前触发
button.RegisterCallback<ClickEvent>(evt =>
{
    HandleClick();
    evt.StopPropagation();
}, TrickleDown.TrickleDown);
```

### 11.3 OnDisable 中的事件注销

`clicked` 用 `+=` 注册，必须用 `-=` 注销；`RegisterCallback` 用 `UnregisterCallback` 注销。否则 GameObject 重复 Enable/Disable 时回调叠加：

```csharp
void OnEnable()
{
    btn.clicked += OnClick;
    btn.RegisterCallback<ClickEvent>(OnClickTrickle, TrickleDown.TrickleDown);
}

void OnDisable()
{
    btn.clicked -= OnClick;
    btn.UnregisterCallback<ClickEvent>(OnClickTrickle);
}
```

---

## 十一（续）、Toggle 与 RadioButton 勾选状态视觉修复

### Toggle 选中状态没有勾号图标

**问题：** 自定义 `.unity-toggle__checkmark` 的背景色后，Unity 默认的勾形图标被覆盖，选中状态只是纯色方块，无法区分选中/未选中。

**根本原因：** Unity UIToolkit 的 Toggle 勾选框使用内置背景图片（`toggle-check`）渲染勾号。当 USS 中只设置 `background-color` 而不处理 `background-image` 时，默认图片被保留但被颜色覆盖，导致勾号不可见。

**正确写法：**

```css
/* 未选中：清除背景图，显示空框 */
.field-toggle > .unity-toggle__input > .unity-toggle__checkmark {
    background-color: #1e2535;
    background-image: none;                          /* 明确清除图片 */
    border-width: 1px;
    border-color: #303a50;
    border-radius: 3px;
    width: 16px;
    height: 16px;
    -unity-background-image-tint-color: rgba(0,0,0,0);
}

/* 选中：加载内置勾形图标，白色显示在橙色背景上 */
.field-toggle > .unity-toggle__input:checked > .unity-toggle__checkmark {
    background-color: #f48120;
    background-image: resource("unity-builtin-extra/toggle-check"); /* 内置勾形图 */
    border-color: #f48120;
    -unity-background-image-tint-color: #ffffff;     /* 图标白色 */
    background-size: contain;
}
```

**关键点：**
- `background-image: none` 在未选中时必须显式设置，否则残留默认纹理
- `resource("unity-builtin-extra/toggle-check")` 引用 Unity 内置勾形图标
- `-unity-background-image-tint-color: #ffffff` 将图标着色为白色，叠在橙色背景上清晰可见

### RadioButton 选中圆点不居中

**问题：** 自定义 `.unity-radio-button__checkmark` 的尺寸后，内圆点停留在父容器左上角，而非居中显示。

**根本原因：** `.unity-radio-button__checkmark` 默认是 `position: relative` 且未设置居中定位，缩小尺寸后不会自动居中。

**正确写法：**

```css
.unity-radio-button:checked > .unity-radio-button__input >
.unity-radio-button__checkmark-background > .unity-radio-button__checkmark {
    background-color: #f48120;
    border-radius: 5px;
    width: 8px;
    height: 8px;
    position: absolute;   /* 绝对定位 */
    top: 3px;             /* (16px外框 - 8px内点) / 2 = 4px，微调为3px视觉居中 */
    left: 3px;
}
```

**计算方式：** 外框 16px，内点 8px，理论居中偏移 = (16-8)/2 = 4px。可微调 ±1px 达到视觉最佳效果。

---

## 十二、RadioButtonGroup 使用注意事项

### 12.1 RadioButton 分组范围规则

Unity 2022 UIToolkit 中，`RadioButton` 的互斥分组范围取决于包裹容器：

| 情况 | 分组范围 | 说明 |
|---|---|---|
| 被 `RadioButtonGroup` 包裹 | 该 `RadioButtonGroup` 内部 | ✅ 推荐，范围明确，API 完整 |
| 裸 `RadioButton`（无 `RadioButtonGroup`） | **最近的直接父容器**内 | ✅ 同父容器内互斥，不同父容器间互不干扰 |

**关键结论：裸 RadioButton 不会跨 `<ui:Instance>` 页面干扰**

每个 `<ui:Instance>` 模板都有自己独立的根 `TemplateContainer`，因此各页面的裸 RadioButton 天然隔离，**即不用 `RadioButtonGroup`，各页面间也不会相互影响**。

但**仍推荐使用 `RadioButtonGroup`**，原因：
1. 提供整数 `value` 属性，直接读写选中索引，无需逐一查询每个 RadioButton
2. 提供统一的 `RegisterValueChangedCallback`（返回 int），一个回调处理整组变化
3. 代码量更少，语义更清晰

```xml
<!-- 裸 RadioButton：同父容器内互斥，跨 <ui:Instance> 页面不干扰 -->
<!-- 但需在 C# 中逐一查询每个 RadioButton.value（bool）-->
<ui:VisualElement class="radio-group">
    <ui:RadioButton name="radio-a" label="选项A" />
    <ui:RadioButton name="radio-b" label="选项B" />
</ui:VisualElement>

<!-- ✅ RadioButtonGroup：推荐写法，提供 value（int 索引）和统一回调 -->
<ui:RadioButtonGroup name="my-group" value="0">
    <ui:RadioButton label="选项A" class="radio-item" />
    <ui:RadioButton label="选项B" class="radio-item" />
    <ui:RadioButton label="选项C" class="radio-item" />
</ui:RadioButtonGroup>
```

**两种写法的 C# 对比：**

```csharp
// 裸 RadioButton：需逐一注册，代码繁琐
var radioA = root.Q<RadioButton>("radio-a");
var radioB = root.Q<RadioButton>("radio-b");
radioA.RegisterValueChangedCallback(e => { if (e.newValue) SetMode(0); });
radioB.RegisterValueChangedCallback(e => { if (e.newValue) SetMode(1); });

// ✅ RadioButtonGroup：一个回调，直接返回选中索引（int）
var group = root.Q<RadioButtonGroup>("my-group");
group.RegisterValueChangedCallback(e => SetMode(e.newValue));
```

### 12.2 RadioButtonGroup 的 value 属性

`value` 属性表示**默认选中项的索引（0-based）**，不是字符串值：

```xml
<!-- value="0" 表示默认选中第一项（索引0）-->
<ui:RadioButtonGroup name="ping-mode-group" value="0">
    <ui:RadioButton label="ICMP Auto（默认）" />  <!-- index 0，默认选中 -->
    <ui:RadioButton label="TCPing" />              <!-- index 1 -->
    <ui:RadioButton label="HTTPing" />             <!-- index 2 -->
</ui:RadioButtonGroup>
```

### 12.3 C# 中监听 RadioButtonGroup 变化

```csharp
// ✅ 监听选中索引变化（返回 int）
var group = root.Q<RadioButtonGroup>("ping-mode-group");
group.RegisterValueChangedCallback(evt =>
{
    int selectedIndex = evt.newValue;
    switch (selectedIndex)
    {
        case 0: opts.PingMode = PingMode.IcmpAuto; break;
        case 1: opts.PingMode = PingMode.TcPing;   break;
        case 2: opts.PingMode = PingMode.Httping;  break;
    }
});

// 读取当前选中索引
int current = group.value;

// 代码设置选中项
group.value = 1; // 选中第二项
```

### 12.4 本项目各界面 RadioButtonGroup 默认值核查

| 页面 | 控件名 | UXML value | 对应需求默认值 | 状态 |
|---|---|---|---|---|
| 延迟测速 | `ping-mode-group` | `value="0"` | `PingMode.IcmpAuto`（第0项） | ✅ 正确 |
| 定时调度 | `sched-mode-group` | `value="0"` | `ScheduleMode.None`（第0项） | ✅ 正确 |

两个 RadioButtonGroup 均已正确设置默认选中项，与需求文档一致。

---

## 十三、页面切换（display:none/flex）注意事项

### 13.1 `<ui:Instance>` 与 TemplateContainer

UXML 中的 `<ui:Instance template="...">` 会被 UIToolkit 包裹在 `TemplateContainer` 中。`name` 和 `class` 属性设置在 `TemplateContainer` 上，通过 `root.Q<VisualElement>("page-ip")` 可正确查到。

```xml
<!-- UXML 中 -->
<ui:Instance template="PageIpSource" name="page-ip" class="page page--active" />

<!-- 运行时实际结构（LayoutDump 所见） -->
<!-- [TemplateContainer] #page-ip .page.page--active -->
<!--   └── ... PageIpSource 的内容 ... -->
```

### 13.2 页面显示/隐藏的正确方式

通过 CSS class 切换 `display: none` 和 `display: flex` 是最高效的方式，不涉及 VisualElement 的创建/销毁：

```css
/* USS */
.page {
    display: none;
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
}
.page--active {
    display: flex;
    flex-direction: column;
}
```

```csharp
// C# 切换页面
pages[i].AddToClassList("page--active");     // 显示
pages[i].RemoveFromClassList("page--active"); // 隐藏
```

**注意：** `display: none` 的元素不参与布局计算，子元素 size 全为 0×0，这是正常现象。

### 13.3 所有页面在启动时全部创建

`<ui:Instance>` 是**静态引用**，UIDocument 加载时所有页面的完整 UI 树立即实例化，页面切换只是 CSS display 的切换，**不涉及创建/销毁 VisualElement**，开销极小。

---

## 十四、元素查询（Q/Query）最佳实践

### 14.1 限定查询范围，避免跨模板误匹配

`root.Q<Button>("name")` 会**递归搜索整个 UI 树**，包括所有 `<ui:Instance>` 内部。若多个模板中存在同名元素，只会返回第一个匹配，可能不是预期的那个。

```csharp
// ❌ 全局搜索，可能匹配到其他模板内的同名元素
var btn = root.Q<Button>("nav-ip");

// ✅ 限定父容器范围搜索
var navList = root.Q<VisualElement>("nav-list");
var btn = navList?.Q<Button>("nav-ip");

// ✅ 限定页面容器范围搜索
var pageContainer = root.Q<VisualElement>("page-container");
var page = pageContainer?.Q<VisualElement>("page-ip");
```

### 14.2 查询时机：OnEnable 而非 Awake

UIDocument 的 `rootVisualElement` 在 `Awake` 时通常可用，但建议在 `OnEnable` 中执行查询和事件绑定，确保 UI 树完全就绪：

```csharp
void Awake()
{
    _doc  = GetComponent<UIDocument>();
    _root = _doc.rootVisualElement; // 仅获取根节点
}

void OnEnable()
{
    // ✅ 在 OnEnable 中查询子元素和绑定事件
    BindElements();
    BindNav();
    BindButtons();
}
```

---

## 十五、布局调试：LayoutDump 技术

### 15.1 LayoutDump 是什么

LayoutDump 是在运行时遍历 UIDocument 的 VisualElement 树，打印每个元素的布局信息（位置、尺寸、显示状态、CSS 类等）的调试工具。对于排查以下问题极为有效：

- 元素为何显示在错误位置
- 元素为何尺寸为 0×0
- 哪个元素遮挡了点击区域
- ScrollView 内容是否真的超出容器

### 15.2 典型 LayoutDump 输出解读

```
[TemplateContainer] #page-ip .page.page--active
  layout : pos=(0,0) size=(708 x 572)      ← 位置和尺寸
  visible: True  opacity=1.00  display=Flex ← 显示状态
  flex   : dir=Column grow=1.0             ← flex 布局参数
```

**关键字段含义：**

| 字段 | 说明 |
|---|---|
| `pos=(x,y)` | 相对父容器的位置 |
| `size=(w x h)` | 实际布局尺寸，0×0 表示不参与布局或被隐藏 |
| `display=None` | 对应 `display: none`，元素不可见也不占空间 |
| `display=Flex` | 元素可见，参与 flex 布局 |
| `visible=False` | `visibility: hidden`，不可见但占空间 |
| `opacity=0.50` | 半透明，通常因父元素 `unity-disabled` 类 |

### 15.3 通过 LayoutDump 诊断 ScrollView 点击问题

```
[VisualElement] #nav-scroll .nav-scroll
  layout : size=(191 x 362)      ← ScrollView 容器高度 362
  [VisualElement] #nav-list .nav-list
    layout : size=(191 x 395)    ← 内容高度 395 > 362
```

上例中 nav-list 高度（395）超过 nav-scroll（362），ScrollView 进入可滚动状态，会拦截 Button 的指针事件。

### 15.4 LayoutDump 实现参考

```csharp
public static void DumpLayout(UIDocument doc)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("===== 运行时 UIDocument 布局 =====");
    DumpElement(doc.rootVisualElement, sb, 0);
    Debug.Log(sb.ToString());
}

static void DumpElement(VisualElement el, System.Text.StringBuilder sb, int depth)
{
    string indent = new string(' ', depth * 2);
    var layout = el.layout;
    sb.AppendLine($"{indent}[{el.GetType().Name}] #{el.name} {string.Join(".", el.GetClasses())}");
    sb.AppendLine($"{indent}  layout : pos=({layout.x:F0},{layout.y:F0}) size=({layout.width:F0} x {layout.height:F0})");
    sb.AppendLine($"{indent}  visible: {el.visible}  display={el.resolvedStyle.display}");
    foreach (var child in el.Children())
        DumpElement(child, sb, depth + 1);
}
```

---

## 十六、页面内容超出导致 flex 压缩问题

### 16.1 问题现象

页面内多个 group-box 的高度被压缩为极小值（如 42px、6px），内容溢出或几乎不可见。

### 16.2 根本原因

页面根元素（TemplateContainer）的高度是固定的（如 572px），若页面内容总高度超出，flex 布局会**按比例压缩所有子元素**，而不是滚动。

**错误结构（内容直接堆叠，无 ScrollView）：**

```xml
<!-- ❌ 内容超出后 flex 压缩所有 group-box -->
<ui:Label text="页面标题" class="page-title" />
<ui:VisualElement class="page-divider" />
<ui:VisualElement class="group-box">...很多内容...</ui:VisualElement>
<ui:VisualElement class="group-box">...很多内容...</ui:VisualElement>
```

**正确结构（内容放入 ScrollView）：**

```xml
<!-- ✅ page-title + page-divider + ScrollView 三层结构 -->
<ui:Label text="页面标题" class="page-title" />
<ui:VisualElement class="page-divider" />
<ui:ScrollView mode="Vertical" class="page-scroll" vertical-scroller-visibility="Auto">
    <ui:VisualElement class="group-box">...内容...</ui:VisualElement>
    <ui:VisualElement class="group-box">...内容...</ui:VisualElement>
</ui:ScrollView>
```

**本项目所有页面均采用此三层结构，是 UIToolkit 页面布局的标准做法。**

### 16.3 诊断指标

通过 LayoutDump 发现以下情况时，说明存在 flex 压缩：
- group-box 的 `size` 远小于其内容所需高度
- form-row 的高度为 0 或极小值
- 按钮高度（如 12px）远小于正常值（如 34px）

---

*文档更新日期：2026-03-17*
