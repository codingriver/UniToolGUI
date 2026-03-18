# Unity UIToolkit 新手入门教程

> 面向完全不了解 Web/CSS 开发的 Unity 开发者。
> 本教程基于项目 `UniToolGUI` 的实际代码讲解，所有示例均来自真实代码。

---

## 目录

1. UIToolkit 是什么
2. 三个核心文件：UXML / USS / C#
3. UXML 节点详解
4. USS 样式详解
5. C# 代码操作 UI
6. 生命周期——各阶段做什么
7. 常用操作速查
8. 事件系统
9. 动态增删元素
10. 本项目完整代码解读

---

## 一、UIToolkit 是什么

### 和 UGUI 的区别


| 对比项  | UGUI（旧方式）             | UIToolkit（新方式）          |
| ---- | --------------------- | ----------------------- |
| 界面文件 | 在 Scene 里拖 GameObject | 独立的 `.uxml` 文本文件        |
| 样式控制 | 每个组件单独设置属性            | 统一的 `.uss` 样式文件         |
| 布局方式 | RectTransform 手动定位    | Flexbox 自动布局            |
| 代码查找 | `GameObject.Find()`   | `root.Q<Label>("name")` |
| 性能   | 每个元素是 GameObject      | 纯数据结构，GPU 批次更少          |


### 三个核心文件的关系

```
界面 = UXML（结构）+ USS（样式）+ C#（逻辑）

  就像盖房子：
  UXML = 房间布局图纸（有几个房间，怎么排列）
  USS  = 装修风格（墙壁颜色、家具大小）
  C#   = 住在里面的人（控制开关、响应操作）
```

---

## 二、UXML 文件详解

### 2.1 文件头（固定写法，不用修改）

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements"
         xmlns:uie="UnityEditor.UIElements"
         editor-extension-mode="False">
```

- `xmlns:ui` — 声明 `ui:` 前缀代表 UIElements 命名空间
- `editor-extension-mode="False"` — 运行时模式

### 2.2 引入样式文件

```xml
<Style src="MainWindow.uss" />                        <!-- 相对路径 -->
<Style src="/Assets/GlobalStyles/GlobalFont.uss" />   <!-- 绝对路径 -->
```

一个 UXML 可以引入多个 USS，后引入的优先级更高。

### 2.3 引入子页面模板

```xml
<!-- 第一步：声明模板（相当于导入文件）-->
<ui:Template name="PageIpSource" src="Pages/PageIpSource.uxml" />

<!-- 第二步：在需要的地方使用 -->
<ui:Instance template="PageIpSource" name="page-ip" class="page page--active" />
```

`Template` + `Instance` 相当于「定义组件 + 使用组件」，避免重复写相同结构。

### 2.4 所有常用 UXML 节点说明

#### VisualElement — 最基础的容器

```xml
<ui:VisualElement name="sidebar" class="sidebar">
    <!-- 子元素放在这里 -->
</ui:VisualElement>
```

- 相当于 HTML 的 `<div>`，纯容器，本身不显示内容
- 用途：分组、布局、背景色区域、进度条轨道等
- C# 类型：`VisualElement`

#### Label — 文本标签

```xml
<ui:Label name="status-text" text="就绪" class="status-text" />
```

- 显示一段不可编辑的文字
- `text` 属性是显示内容
- C# 修改：`label.text = "新内容";`

#### Button — 按钮

```xml
<ui:Button name="btn-start" text="▶  开始测速" class="btn-start" />
```

- 可点击的按钮，`text` 是按钮上的文字
- C# 监听点击：`btn.RegisterCallback<ClickEvent>(e => { });`

#### TextField — 文本输入框

```xml
<!-- 单行 -->
<ui:TextField name="field-ipv4" value="ip.txt" class="field-text" />

<!-- 多行 -->
<ui:TextField name="field-log" multiline="true" class="field-text field-text--multiline" />
```

- 用户可输入文字，`value` 是初始值
- C# 读取：`textField.value`
- C# 设置：`textField.value = "新值";`

#### Toggle — 勾选框（独立开关）

```xml
<ui:Toggle name="toggle-allip" label="全量扫描" class="field-toggle" />
```

- 表示**某个选项的开/关**，相互独立，互不影响
- `label` 是旁边的文字；`value` 是 bool（true=勾选，false=未勾选）
- 每个 Toggle 完全独立，勾选 A 不影响 B
- C# 读取状态：`toggle.value`（bool）
- C# 监听变化：`toggle.RegisterValueChangedCallback(e => Debug.Log(e.newValue));`
- **典型用途**：启用/禁用某功能（如「禁用下载测速」「全量扫描」「强制 ICMP」）

#### RadioButton / RadioButtonGroup — 单选组（多选一）

```xml
<!-- 单独使用：同一父容器内互斥 -->
<ui:RadioButton name="radio-a" label="选项A" />
<ui:RadioButton name="radio-b" label="选项B" />

<!-- 推荐：用 RadioButtonGroup 包裹，明确分组 + 更好用的 API -->
<ui:RadioButtonGroup name="ping-mode-group" value="0">
    <ui:RadioButton label="ICMP Auto（默认）" class="radio-item" />
    <ui:RadioButton label="TCPing" class="radio-item" />
    <ui:RadioButton label="HTTPing" class="radio-item" />
</ui:RadioButtonGroup>
```

- 表示**多个选项中只能选一个**，选中一个后其他自动取消
- `RadioButtonGroup` 的 `value` 属性是默认选中项的**索引**（0-based），`value="0"` 表示默认选第一项
- C# 读取当前选中索引：`group.value`（int）
- C# 监听变化：`group.RegisterValueChangedCallback(e => Debug.Log(e.newValue));`（返回 int 索引）
- **典型用途**：测速方式（ICMP / TCPing / HTTPing 三选一）、调度模式（不启用 / 间隔 / 定点 / Cron 四选一）

**Toggle 与 RadioButton 的核心区别：**

| 控件 | 语义 | 互斥 | value 类型 | 典型场景 |
|---|---|---|---|---|
| `Toggle` | 某功能开/关 | 否，各自独立 | `bool` | 「启用 HTTPS」「全量扫描」|
| `RadioButton` | 多选一 | 是，同组互斥 | `bool`（单个） | 配合 `RadioButtonGroup` 使用 |
| `RadioButtonGroup` | 单选组容器 | 自动管理互斥 | `int`（索引）| 「选择测速方式」|

#### IntegerField — 整数输入框

```xml
<ui:IntegerField name="field-iploadlimit" value="0" class="field-int" />
```

- 只能输入整数
- C# 读取：`intField.value`（int）

#### ScrollView — 可滚动容器

```xml
<ui:ScrollView name="nav-scroll" class="nav-scroll"
               mode="Vertical"
               vertical-scroller-visibility="AlwaysVisible">
    <!-- 内容放在这里，超出时可滚动 -->
</ui:ScrollView>
```

- `mode`：`Vertical`（竖向）/ `Horizontal`（横向）
- `vertical-scroller-visibility`：`Auto` / `AlwaysVisible` / `Hidden`

### 2.5 name 和 class 的区别

```xml
<ui:Label name="status-text" class="status-text sb-item" />
              ↑ C# 查找用       ↑ USS 样式用，可多个，空格分隔
```


| 属性      | 作用        | USS 选择器            | C# 查找方式                       |
| ------- | --------- | ------------------ | ----------------------------- |
| `name`  | 唯一标识符     | `#status-text { }` | `root.Q("status-text")`       |
| `class` | 样式分类（可多个） | `.status-text { }` | `root.Q(null, "status-text")` |


---

## 三、USS 样式详解

### 3.1 USS 基本格式

```css
/* 格式：选择器 { 属性: 值; } */
.btn-start {
    background-color: #f48120;   /* 背景色 */
    color: #0f1117;              /* 文字颜色 */
    border-radius: 6px;          /* 圆角 */
    padding: 10px 0;             /* 内边距 */
    font-size: 13px;             /* 字号 */
}
```

### 3.2 选择器写法

```css
/* 按 class 选中（最常用）*/
.nav-item { color: #9aa3b5; }

/* 按 name 选中 */
#btn-start { background-color: #f48120; }

/* 后代选择器：.sidebar 内部所有 .nav-item */
.sidebar .nav-item { font-size: 13px; }

/* 鼠标悬停状态 */
.nav-item:hover { background-color: #1e2535; }

/* 同时作用于多个选择器 */
.btn-start, .btn-stop { border-width: 0; }
```

### 3.3 常用属性速查

#### 尺寸

```css
.my-element {
    width: 192px;        /* 固定宽度 */
    height: 100%;        /* 占父容器 100% 高度 */
    min-width: 100px;    /* 最小宽度 */
    flex-grow: 1;        /* 自动占满剩余空间 */
}
```

#### 颜色

```css
.my-element {
    background-color: #161b24;               /* 背景色（十六进制）*/
    background-color: rgba(244,129,32,0.15); /* 背景色（带透明度）*/
    color: #e8eaf0;                          /* 文字颜色 */
    border-color: #252d3d;                   /* 边框颜色 */
}
```

#### 边框

```css
.my-element {
    border-width: 1px;        /* 四边边框宽度 */
    border-left-width: 3px;   /* 只设左边框 */
    border-color: #f48120;    /* 边框颜色 */
    border-radius: 6px;       /* 圆角 */
}
```

#### 间距

```css
.my-element {
    padding: 10px;                    /* 内边距，四边相同 */
    padding: 18px 16px 14px 16px;    /* 上 右 下 左 */
    margin: 6px;                      /* 外边距，四边相同 */
    margin-bottom: 8px;               /* 只设底部 */
}
```

#### 布局（Flexbox）

```css
/* 父容器：设置子元素排列方向 */
.sidebar      { flex-direction: column; }  /* 子元素从上到下 */
.root         { flex-direction: row; }     /* 子元素从左到右 */

/* 子元素：占满剩余空间 */
.nav-scroll   { flex-grow: 1; }   /* 占满父容器剩余高度/宽度 */
.content-area { flex-grow: 1; }

/* 对齐方式 */
.logo-area {
    align-items: center;              /* 垂直居中 */
    justify-content: space-between;   /* 水平两端对齐 */
}
```

**Flexbox 图示：**

```
flex-direction: row（横向排列）
┌──────────────────────────────┐
│ [sidebar 192px] [content:1]  │
└──────────────────────────────┘

flex-direction: column（纵向排列）
┌─────────────┐
│ [logo-area] │
│ [nav-scroll:1，占满剩余] │
│ [sidebar-bottom] │
└─────────────┘

flex-grow: 1 的效果：
┌──────────────────────────────────┐
│ [固定宽度] │ [flex-grow:1 占满]  │
└──────────────────────────────────┘
```

#### 显示与隐藏

```css
/* 隐藏（不占空间）*/
.result-badge--hidden { display: none; }
/* 显示 */
.result-badge { display: flex; }
```

---

## 四、C# 代码操作 UI

### 4.1 场景搭建

1. 新建空 GameObject，命名如 `UIManager`
2. 添加组件 `UIDocument`
3. 将 `.uxml` 文件拖到 `UIDocument` 的 `Source Asset` 字段
4. 新建 C# 脚本挂在同一 GameObject 上，加 `[RequireComponent(typeof(UIDocument))]`

### 4.2 获取根节点（必须第一步）

```csharp
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MyController : MonoBehaviour
{
    private UIDocument    _doc;
    private VisualElement _root;

    private void Awake()
    {
        _doc  = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;  // 所有查找都从这里开始
    }
}
```

### 4.3 查找元素——Q() 方法

```csharp
// 按 name 查找（UXML 中 name="btn-start"）
Button btnStart = _root.Q<Button>("btn-start");
Label  statusLbl = _root.Q<Label>("status-text");

// 按 class 查找（返回第一个匹配的）
Button navBtn = _root.Q<Button>(null, "nav-item");

// 查找所有匹配的元素（返回列表）
var allNavBtns = _root.Query<Button>(null, "nav-item").ToList();

// 不指定类型，返回 VisualElement
VisualElement fill = _root.Q("progress-fill");

// 找不到返回 null，用 ?. 安全访问
btnStart?.SetEnabled(false);
```

**本项目实际写法（MainWindowController.cs）：**

```csharp
private void BindElements()
{
    _btnStart      = _root.Q<Button>("btn-start");
    _btnStop       = _root.Q<Button>("btn-stop");
    _progressFill  = _root.Q<VisualElement>("progress-fill");
    _progressPct   = _root.Q<Label>("progress-pct");
    _sidebarStatus = _root.Q<Label>("status-text");
    _resultBadge   = _root.Q<Label>("result-badge");
}
```

---

## 五、生命周期——各阶段做什么

```
Awake()     → 获取组件、获取根节点（不查 UI 元素）
OnEnable()  → 查找元素、绑定事件、初始化数据
Update()    → 尽量不用，改用事件驱动
OnDisable() → 注销事件、释放资源
```

```csharp
private void Awake()
{
    _doc  = GetComponent<UIDocument>();
    _root = _doc.rootVisualElement;
    // 不要在 Awake 查 UI 元素！UXML 可能未就绪
}

private void OnEnable()
{
    BindElements();                                     // 查找元素
    _btnStart?.RegisterCallback<ClickEvent>(_ => StartTest()); // 绑定事件
    AppState.Instance.OnChanged += RefreshSidebar;      // 订阅数据变化
    RefreshSidebar();                                   // 初始刷新
}

private void OnDisable()
{
    AppState.Instance.OnChanged -= RefreshSidebar;  // 必须注销！
    _procManager?.Dispose();
}
```

**为什么不在 Update 里刷新 UI？**

```csharp
// 不推荐：每帧都写，即使数据没变化
private void Update() { _label.text = data.Value; }

// 推荐：数据变化时才触发（事件驱动）
// 数据类：
public event Action OnChanged;
private string _value;
public string Value
{
    get => _value;
    set { _value = value; OnChanged?.Invoke(); }  // 变化时触发事件
}
// UI 中：
data.OnChanged += () => _label.text = data.Value;
```

---

## 六、常用操作速查

### 6.1 文本读写

```csharp
_label.text       = "就绪";           // Label
_btn.text         = "▶ 开始";         // Button
_textField.value  = "ip.txt";         // TextField 写
string s          = _textField.value; // TextField 读
_intField.value   = 100;              // IntegerField 写
int n             = _intField.value;  // IntegerField 读
bool b            = _toggle.value;    // Toggle 读（true=勾选）
_toggle.value     = true;             // Toggle 写（代码设置勾选状态）
int idx           = _radioGroup.value; // RadioButtonGroup 读（选中项索引）
_radioGroup.value = 1;                // RadioButtonGroup 写（选中第二项）
```

### 6.2 尺寸

```csharp
element.style.width  = 192;          // 固定像素
element.style.height = 30;

// 百分比（本项目进度条实际写法）
_progressFill.style.width =
    new StyleLength(new Length(75f, LengthUnit.Percent));

element.style.width = StyleKeyword.Auto;  // 恢复自动
```

### 6.3 显示隐藏

```csharp
element.style.display = DisplayStyle.None;  // 隐藏（不占空间）
element.style.display = DisplayStyle.Flex;  // 显示

// 通过 class 控制（推荐，样式逻辑分离）
element.AddToClassList("result-badge--hidden");      // 隐藏
element.RemoveFromClassList("result-badge--hidden"); // 显示
```

### 6.4 启用禁用

```csharp
_btnStart.SetEnabled(false); // 禁用（变灰，无法点击）
_btnStart.SetEnabled(true);  // 启用
```

### 6.5 class 操作（状态切换）

```csharp
element.AddToClassList("active");       // 添加
element.RemoveFromClassList("active");  // 移除
element.ToggleInClassList("active");    // 切换
bool has = element.ClassListContains("active"); // 检查
```

### 6.6 颜色

```csharp
ColorUtility.TryParseHtmlString("#f48120", out Color c);
element.style.backgroundColor = new StyleColor(c);
label.style.color = new StyleColor(Color.white);
```

---

## 七、事件系统

### 7.1 按钮点击

```csharp
// RegisterCallback（推荐，可注销）
_btnStart.RegisterCallback<ClickEvent>(evt => StartTest());

// clicked 委托（Button 专用，更简洁）
_btnStart.clicked += () => StartTest();
```

### 7.2 输入框变化

```csharp
_textField.RegisterValueChangedCallback(evt =>
{
    Debug.Log($"旧值: {evt.previousValue}  新值: {evt.newValue}");
});

// Toggle：bool 值变化（独立开关，互不影响）
_toggle.RegisterValueChangedCallback(evt =>
{
    bool isOn = evt.newValue;  // true = 勾选，false = 取消
});

// RadioButtonGroup：选中项索引变化（多选一，同组互斥）
_radioGroup.RegisterValueChangedCallback(evt =>
{
    int selectedIndex = evt.newValue;  // 选中项的索引（0-based）
    // 注意：Toggle 返回 bool，RadioButtonGroup 返回 int
    switch (selectedIndex)
    {
        case 0: /* 第一项 */ break;
        case 1: /* 第二项 */ break;
    }
});

_intField.RegisterValueChangedCallback(evt =>
{
    int value = evt.newValue;
});
```

### 7.3 注销事件（OnDisable 中必须做）

```csharp
// Lambda 无法注销，改用具名方法
private void OnStartClick(ClickEvent evt) => StartTest();
private void OnEnable()  { _btnStart.RegisterCallback<ClickEvent>(OnStartClick); }
private void OnDisable() { _btnStart?.UnregisterCallback<ClickEvent>(OnStartClick); }

// clicked 委托方式
private void OnEnable()  { _btnStart.clicked += StartTest; }
private void OnDisable() { _btnStart.clicked -= StartTest; }
private void StartTest() { }
```

### 7.4 键盘事件

```csharp
_textField.RegisterCallback<KeyDownEvent>(evt =>
{
    if (evt.keyCode == KeyCode.Return) SubmitForm();
});
```

---

## 八、动态增删元素

```csharp
// 创建并添加 Label
var label = new Label("动态文字");
label.AddToClassList("table-cell");
_container.Add(label);

// 创建并添加 Button
var btn = new Button(() => Debug.Log("点击"));
btn.text = "动态按钮";
btn.AddToClassList("btn-sm");
_container.Add(btn);

// 清空容器
_container.Clear();

// 删除单个元素
element.RemoveFromHierarchy();

// 在指定位置插入
_container.Insert(0, element);  // 插入到第一个位置
```

---

## 九、本项目完整代码解读

### 9.1 项目 UI 树形结构

```
MainWindow.uxml
├── root（整个窗口，flex-direction: row）
│   ├── sidebar（左侧导航栏，固定 192px 宽）
│   │   ├── logo-area（Logo + 标题）
│   │   ├── nav-scroll（ScrollView，flex-grow:1）
│   │   │   └── nav-list → 8个导航 Button
│   │   └── sidebar-bottom（开始/停止/进度条）
│   │       ├── btn-start（Button）
│   │       ├── btn-stop（Button）
│   │       ├── progress-track → progress-fill（进度条）
│   │       ├── progress-pct（Label，百分比）
│   │       └── status-text（Label，状态文字）
│   └── content-area（右侧，flex-grow:1）
│       ├── page-container（8个子页面，同时只显示1个）
│       │   ├── page-ip（PageIpSource，默认显示）
│       │   └── page-latency / download / ... （默认隐藏）
│       └── statusbar（底部状态栏）
│           ├── sb-status / sb-tested / sb-elapsed / sb-best
```

### 9.2 初始化流程

```csharp
// Awake：获取根节点
_doc  = GetComponent<UIDocument>();
_root = _doc.rootVisualElement;

// OnEnable：完整初始化
BindElements();  // 查找并缓存所有 UI 元素
InitPages();     // 把根节点传给各子页面控制器
BindNav();       // 导航按钮注册点击事件
BindButtons();   // 开始/停止按钮注册点击事件
AppState.Instance.OnChanged += RefreshSidebar;  // 订阅状态变化
NavigateTo(0);   // 默认显示第一页
RefreshSidebar(); // 刷新初始状态
```

### 9.3 进度条更新（运行时修改样式的典型案例）

```csharp
// USS 中进度条初始宽度为 0%：
// .progress-fill { width: 0%; background-color: #f48120; }

// C# 运行时动态修改宽度：
private void RefreshSidebar()
{
    float pct = AppState.Instance.Progress * 100f;  // 0~100

    _progressFill.style.width =
        new StyleLength(new Length(pct, LengthUnit.Percent));

    _progressPct.text = $"{pct:F0}%";
    _sidebarStatus.text = AppState.Instance.StatusText;
}
```

### 9.4 页面切换（class 控制显示/隐藏）

```css
/* USS：默认隐藏所有页面 */
.page          { display: none; }
.page--active  { display: flex; flex-direction: column; }

/* USS：导航按钮默认和激活状态 */
.nav-item          { color: #9aa3b5; background-color: transparent; }
.nav-item--active  { color: #f48120; background-color: rgba(244,129,32,0.15); }
```

```csharp
// C#：切换时操作 class
public void NavigateTo(int idx)
{
    for (int i = 0; i < PAGE_COUNT; i++)
    {
        bool active = (i == idx);
        if (active) _pages[i].AddToClassList("page--active");
        else        _pages[i].RemoveFromClassList("page--active");
        if (active) _navBtns[i].AddToClassList("nav-item--active");
        else        _navBtns[i].RemoveFromClassList("nav-item--active");
    }
}
```

### 9.5 跨线程更新 UI

```csharp
// 进程输出回调在子线程，必须切回主线程更新 UI
_procManager.OnOutput += line =>
    UnityMainThreadDispatcher.Enqueue(() =>
    {
        // 这里才是主线程，可以安全操作 UI
        PageOther?.AppendLog(line);
        OutputParser.Parse(line, AppState.Instance, TestResult.Instance);
    });
```

---

## 十、常见错误和解决方案


| 错误现象             | 原因                           | 解决方案                                                     |
| ---------------- | ---------------------------- | -------------------------------------------------------- |
| `Q<T>()` 返回 null | name/class 拼写错误，或在 Awake 中查找 | 检查拼写，移到 OnEnable                                         |
| 样式不生效            | USS 未被引入，或被更高优先级覆盖           | 检查 `<Style src=...>`，用 UI Debugger 查看                    |
| 中文显示方块           | 字体资产不含该汉字                    | 重新生成 FontAsset，扩大 Custom Range                           |
| 修改 UI 后崩溃        | 在子线程操作 UI                    | 用 UnityMainThreadDispatcher 切回主线程                        |
| 内存泄漏/事件多次触发      | 未注销事件                        | OnDisable 中注销所有事件                                        |
| 滚动条样式不生效         | 用了 ID 选择器 `#unity-dragger`   | 改用 class `.unity-base-slider__dragger`                   |
| 进度条显示不更新         | 直接设 style.width 数字没加单位       | 用 `new StyleLength(new Length(pct, LengthUnit.Percent))` |
| Toggle 选中看不到勾号   | 只设了 `background-color`，内置图标被覆盖 | 加 `background-image: resource("unity-builtin-extra/toggle-check")` + `tint-color: #ffffff` |
| RadioButton 圆点不居中 | 内圆点缩小后未设定位，停在左上角 | 给 `.unity-radio-button__checkmark` 加 `position: absolute; top: 3px; left: 3px` |
| RadioButton 点击无效被 ScrollView 拦截 | ScrollView 可滚动时消耗 PointerDown 事件 | 用 `RegisterCallback<ClickEvent>(cb, TrickleDown.TrickleDown)` |


---

## 十一、快速上手清单

做一个新界面的完整步骤：

```
1. 在 Assets/UI/ 新建 MyPanel.uxml
2. 在 Assets/UI/ 新建 MyPanel.uss
3. UXML 顶部加 <Style src="MyPanel.uss" />
4. 在 UXML 中写界面结构（Label、Button、TextField...）
5. 每个需要代码操作的元素加 name="xxx"
6. 每个需要样式的元素加 class="xxx"
7. 在 USS 中写 .xxx { } 样式
8. 新建 MyPanelController.cs，加 [RequireComponent(typeof(UIDocument))]
9. Awake 中获取 _root = _doc.rootVisualElement
10. OnEnable 中 Q<T>("name") 查找元素，注册事件
11. OnDisable 中注销事件
12. 场景中建 GameObject，挂 UIDocument + MyPanelController
13. UIDocument 的 Source Asset 选 MyPanel.uxml
14. PanelSettings 的 Theme 选 UnityDefaultRuntimeTheme
```

---

## 十二、选择器详解——ID、Class、后代、子级

### 12.1 ID 选择器（# 开头）

对应 UXML 中的 `name` 属性，全局唯一，用于精确定位某一个元素。

```xml
<!-- UXML -->
<ui:Label name="status-text" text="就绪" />
```

```css
/* USS：# 后面跟 name 的值 */
#status-text {
    color: #e8eaf0;
    font-size: 12px;
}
```

```csharp
// C#：按 name 查找
Label lbl = _root.Q<Label>("status-text");
```

**什么时候用 ID 选择器？**

- 只有这一个元素需要特殊样式（比如 Logo、进度条）
- C# 代码要精确操作某个元素

---

### 12.2 Class 选择器（. 开头）

对应 UXML 中的 `class` 属性，一个元素可以有多个 class，多个元素可以共用同一个 class。

```xml
<!-- 一个元素多个 class，空格分隔 -->
<ui:Label class="sb-item sb-status" text="就绪" />
<ui:Label class="sb-item" text="已测: --" />
<ui:Label class="sb-item" text="耗时: --" />
```

```css
/* USS：. 后面跟 class 名 */
/* 所有有 sb-item class 的元素都应用这个样式 */
.sb-item {
    font-size: 11px;
    color: #9aa3b5;
}

/* 只有同时有 sb-item 和 sb-status 的元素才应用 */
.sb-item.sb-status {
    color: #f48120;  /* 状态文字橙色 */
}
```

**注意：`.sb-item.sb-status`（无空格）表示同时拥有两个 class 的元素，和 `.sb-item .sb-status`（有空格）完全不同！**

```csharp
// C#：按 class 查找第一个匹配
Label first = _root.Q<Label>(null, "sb-item");

// C#：查找所有匹配
var all = _root.Query<Label>(null, "sb-item").ToList();
```

---

### 12.3 后代选择器（空格）

选中某个元素**内部任意层级**的子元素，层级之间用**空格**分隔。

```css
/* 选中 .sidebar 内部所有 .nav-item（不管隔了几层）*/
.sidebar .nav-item {
    font-size: 13px;
}

/* 选中 .unity-scroll-view 内部所有 .unity-base-slider__dragger */
.unity-scroll-view .unity-scroller--vertical .unity-base-slider__dragger {
    background-color: #303a50;
}
```

**结构示意：**

```
.sidebar                       ← 祖先
  └── .nav-scroll              ← 中间层（被跳过也没关系）
        └── .nav-list
              └── .nav-item    ← 后代选择器能找到它 ✅
```

---

### 12.4 直接子选择器（>）

只选中**直接子元素**，不能跨层级。

```css
/* 只选中 .nav-list 的直接子元素中有 .nav-item 的 */
.nav-list > .nav-item {
    margin-bottom: 2px;
}
```

**和后代选择器的区别：**

```
.parent
  ├── .child           ← .parent > .child 能选中 ✅
  │     └── .child     ← .parent > .child 选不中 ❌（隔了一层）
  │                      .parent .child  能选中  ✅（后代）
  └── .child           ← .parent > .child 能选中 ✅
```

**UIToolkit 中的重要提示**：Unity ScrollView 内部结构有隐藏层级（shadow DOM），
滚动条内部元素**必须用后代选择器（空格）**，不能用直接子选择器（`>`）。

---

### 12.5 多 class 同时匹配（无空格连写）

```css
/* 同时有 nav-item 和 nav-item--active 两个 class 才匹配 */
.nav-item.nav-item--active {
    color: #f48120;
    border-left-color: #f48120;
}

/* 对比：有空格是后代选择器，意思完全不同！ */
.nav-item .nav-item--active {   /* .nav-item 内部的 .nav-item--active 子元素 */
    color: #f48120;
}
```

---

### 12.6 伪类选择器（:hover / :active / :focus 等）

```css
/* 鼠标悬停时 */
.nav-item:hover {
    background-color: #1e2535;
    color: #e8eaf0;
}

/* 鼠标按下时 */
.btn-start:active {
    background-color: #d4700f;
}

/* 获得焦点时（输入框被选中）*/
.field-text:focus {
    border-color: #f48120;
}

/* 被禁用时（SetEnabled(false)）*/
.btn-start:disabled {
    opacity: 0.4;
}
```

Unity 2022 UIToolkit 支持的伪类：`:hover`、`:active`、`:focus`、`:checked`、`:disabled`

---

### 12.7 逗号选择器（同时作用多个）

```css
/* 逗号分隔，同一套样式作用于多个选择器 */
.btn-start,
.btn-stop {
    border-width: 0;
    border-radius: 6px;
    font-size: 13px;
}

/* 等价于分开写两条，但更简洁 */
```

---

### 12.8 选择器完整对照表


| 写法           | 名称         | 含义                 | 示例                               |
| ------------ | ---------- | ------------------ | -------------------------------- |
| `.nav-item`  | class 选择器  | 所有有该 class 的元素     | `.nav-item { }`                  |
| `#btn-start` | ID 选择器     | name=btn-start 的元素 | `#btn-start { }`                 |
| `.A .B`      | 后代选择器      | A 内部任意层级的 B        | `.sidebar .nav-item { }`         |
| `.A > .B`    | 直接子选择器     | A 的直接子元素 B         | `.nav-list > .nav-item { }`      |
| `.A.B`       | 多 class 匹配 | 同时有 A 和 B class    | `.nav-item.nav-item--active { }` |
| `.A:hover`   | 伪类         | 鼠标悬停状态             | `.nav-item:hover { }`            |
| `.A, .B`     | 多选         | 同时作用 A 和 B         | `.btn-start, .btn-stop { }`      |


---

## 十三、Flexbox 布局详解

### 13.1 核心概念

Flexbox 布局由**父容器**和**子元素**两部分组成：

- 父容器上设置 `flex-direction`，决定子元素排列方向
- 子元素上设置 `flex-grow`，决定占多少剩余空间

### 13.2 左右布局（本项目 sidebar + content-area）

```xml
<!-- UXML：两个子元素在同一父容器下 -->
<ui:VisualElement class="root">
    <ui:VisualElement class="sidebar" />      <!-- 左 -->
    <ui:VisualElement class="content-area" />  <!-- 右 -->
</ui:VisualElement>
```

```css
/* USS：父容器横向排列 */
.root {
    flex-direction: row;   /* 子元素从左到右 */
    width: 100%;
    height: 100%;
}

/* 左侧固定宽度 */
.sidebar {
    width: 192px;
    min-width: 192px;
}

/* 右侧占满剩余空间 */
.content-area {
    flex-grow: 1;   /* 占满父容器剩余宽度 */
}
```

```
┌────────────────────────────────────────────┐
│ .root（flex-direction: row）               │
│ ┌────────────┬───────────────────────────┐ │
│ │ .sidebar   │ .content-area             │ │
│ │ 192px 固定 │ flex-grow:1 占满剩余       │ │
│ └────────────┴───────────────────────────┘ │
└────────────────────────────────────────────┘
```

### 13.3 上下布局（sidebar 内部）

```css
/* sidebar 内部纵向排列 */
.sidebar {
    flex-direction: column;   /* 子元素从上到下 */
}

/* 导航菜单占满中间剩余高度 */
.nav-scroll {
    flex-grow: 1;
}

/* logo-area 和 sidebar-bottom 高度由内容决定（不设 flex-grow）*/
```

```
┌─────────────┐
│ .sidebar（flex-direction: column）│
│ ┌─────────────────────────────┐  │
│ │ .logo-area（固定高度）       │  │
│ ├─────────────────────────────┤  │
│ │ .nav-scroll（flex-grow:1）  │  │
│ │  占满中间所有剩余空间        │  │
│ ├─────────────────────────────┤  │
│ │ .sidebar-bottom（固定高度）  │  │
│ └─────────────────────────────┘  │
└──────────────────────────────────┘
```

### 13.4 align-items 和 justify-content

```css
/* flex-direction: row 时 */
.logo-area {
    flex-direction: row;
    align-items: center;      /* 垂直居中（交叉轴）*/
    justify-content: center;  /* 水平居中（主轴）*/
}

/* flex-direction: column 时，方向互换 */
.card {
    flex-direction: column;
    align-items: center;      /* 水平居中（交叉轴）*/
    justify-content: center;  /* 垂直居中（主轴）*/
}
```


| `flex-direction` | `justify-content` 控制方向 | `align-items` 控制方向 |
| ---------------- | ---------------------- | ------------------ |
| `row`（横向）        | 水平（左右）                 | 垂直（上下）             |
| `column`（纵向）     | 垂直（上下）                 | 水平（左右）             |


### 13.5 常用布局模板

```css
/* 水平垂直居中 */
.center-box {
    flex-direction: row;
    align-items: center;
    justify-content: center;
}

/* 上中下三段式（header + body + footer）*/
.page {
    flex-direction: column;
    height: 100%;
}
.page-header { height: 48px; }
.page-body   { flex-grow: 1; }  /* 占满中间 */
.page-footer { height: 30px; }

/* 左右两端对齐 */
.toolbar {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
}

/* 固定左侧 + 自适应右侧 */
.split-view {
    flex-direction: row;
}
.split-left  { width: 240px; }
.split-right { flex-grow: 1; }
```

---

## 十四、动态创建元素并添加到界面

### 14.1 最简单的写法

```csharp
// 1. 创建按钮
var btn = new Button();
btn.text = "点击我";

// 2. 找到父容器并添加
var container = _root.Q("nav-list");
container.Add(btn);
```

---

### 14.2 完整写法（含样式、事件、名称）

```csharp
private void AddDynamicButton(string labelText, System.Action onClick)
{
    // 1. 创建 Button
    var btn = new Button();

    // 2. 设置文字
    btn.text = labelText;

    // 3. 设置 name（方便之后用 Q() 查找）
    btn.name = $"btn-{labelText}";

    // 4. 添加 class（复用已有 USS 样式，不需要重写）
    btn.AddToClassList("nav-item");

    // 5. 注册点击事件
    btn.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());

    // 6. 找到父容器并添加
    var container = _root.Q<VisualElement>("nav-list");
    container?.Add(btn);
}
```

调用：

```csharp
AddDynamicButton("新页面", () => Debug.Log("点击了新页面"));
```

---

### 14.3 添加到界面的几种位置方式

```csharp
var container = _root.Q("nav-list");

// 追加到末尾（最常用）
container.Add(btn);

// 插入到最前面（索引 0）
container.Insert(0, btn);

// 插入到某个已有元素之前
var refElement = _root.Q("nav-results");
refElement.parent.Insert(refElement.parent.IndexOf(refElement), btn);

// 直接添加到根节点
_root.Add(btn);
```

---

### 14.4 动态删除元素

```csharp
// 删除单个元素
btn.RemoveFromHierarchy();

// 清空容器内所有子元素
var container = _root.Q("nav-list");
container.Clear();

// 只删除特定 class 的元素
var dynamicBtns = _root.Query<Button>(null, "dynamic-btn").ToList();
foreach (var b in dynamicBtns)
    b.RemoveFromHierarchy();
```

---

### 14.5 结合本项目风格的完整案例

在导航栏动态添加菜单按钮，样式与现有按钮一致：

```csharp
public Button AddNavButton(string text, System.Action onClickAction)
{
    var btn = new Button();
    btn.text = text;
    btn.AddToClassList("nav-item");  // 复用现有 nav-item 样式

    btn.RegisterCallback<ClickEvent>(_ =>
    {
        // 取消其他按钮的激活状态
        foreach (var navBtn in _navBtns)
            navBtn?.RemoveFromClassList("nav-item--active");

        // 激活当前按钮
        btn.AddToClassList("nav-item--active");

        // 执行点击逻辑
        onClickAction?.Invoke();
    });

    // 插入到分隔线之前（如果有），否则追加到末尾
    var navList = _root.Q<VisualElement>("nav-list");
    var divider = navList?.Q(null, "nav-divider");
    if (divider != null)
        navList.Insert(navList.IndexOf(divider), btn);
    else
        navList?.Add(btn);

    return btn;  // 返回引用，方便之后操作
}
```

调用和后续操作：

```csharp
var myBtn = AddNavButton("动态页面", () => Debug.Log("点击了动态页面"));

// 之后随时修改
myBtn.text = "改名了";
myBtn.SetEnabled(false);         // 禁用
myBtn.RemoveFromHierarchy();     // 从界面移除
```

---

### 14.6 常见元素的创建方式

```csharp
// Label
var label = new Label("显示文字");
label.AddToClassList("my-label");
container.Add(label);

// TextField
var field = new TextField("标签");
field.value = "默认值";
field.RegisterValueChangedCallback(e => Debug.Log(e.newValue));
container.Add(field);

// Toggle（独立开关，互不影响）
var toggle = new Toggle("开关文字");
toggle.value = true;  // 默认勾选
toggle.RegisterValueChangedCallback(e => Debug.Log("Toggle: " + e.newValue));
container.Add(toggle);

// RadioButtonGroup + RadioButton（多选一）
var group = new RadioButtonGroup();
group.name = "my-group";
group.Add(new RadioButton { label = "选项A" });
group.Add(new RadioButton { label = "选项B" });
group.Add(new RadioButton { label = "选项C" });
group.value = 0;  // 默认选第一项
group.RegisterValueChangedCallback(e => Debug.Log("选中索引: " + e.newValue));
container.Add(group);

// VisualElement（容器）
var box = new VisualElement();
box.AddToClassList("card");
box.Add(new Label("卡片内容"));
container.Add(box);
```

---

### 14.7 注意事项

| 注意点 | 说明 |
|---|---|
| 必须在主线程执行 | 不能在 `Task.Run` 或子线程里调用 `Add()`，需通过 `UnityMainThreadDispatcher` 切回主线程 |
| 父容器可能为 null | 用 `?.Add()` 安全调用，或先 `if (container != null)` 判断 |
| 样式通过 class 复用 | `AddToClassList("nav-item")` 直接复用已有 USS，不需要重写样式 |
| 动态元素的事件 | 按钮被 `RemoveFromHierarchy()` 后，其自身的 `RegisterCallback` 自动失效；但若订阅了外部 C# 事件（如 `AppState.OnChanged`），仍需手动注销 |

---

*文档更新日期：2026-03-16* 