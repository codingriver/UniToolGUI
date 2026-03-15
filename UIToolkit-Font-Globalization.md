# UIToolkit 字体全球化深度技术文档

> **适用版本**: Unity 2022 LTS (2022.3.x)  
> **涉及模块**: UIToolkit · TextMeshPro · Font Asset · Font Fallback · USS · AssetBundle  
> **渲染管线**: URP / HDRP / Built-in RP 均适用  
> **最后更新**: 2025

---

## 目录

1. [背景与核心挑战](#1-背景与核心挑战)
2. [UIToolkit 字体系统架构](#2-uitoolkit-字体系统架构)
3. [SDF 字体原理深析](#3-sdf-字体原理深析)
4. [字体资产创建详解](#4-字体资产创建详解)
5. [动态字体 vs 静态字体决策](#5-动态字体-vs-静态字体决策)
6. [中文字体集成实战](#6-中文字体集成实战)
7. [字体回退系统 Font Fallback](#7-字体回退系统-font-fallback)
8. [系统字体自动回退](#8-系统字体自动回退)
9. [全球化多语言方案设计](#9-全球化多语言方案设计)
10. [USS 样式中的字体声明](#10-uss-样式中的字体声明)
11. [UXML 与 C# 字体绑定](#11-uxml-与-c-字体绑定)
12. [运行时动态字体加载](#12-运行时动态字体加载)
13. [性能优化深度策略](#13-性能优化深度策略)
14. [常见问题排查手册](#14-常见问题排查手册)
15. [完整项目结构参考](#15-完整项目结构参考)

---

## 1. 背景与核心挑战

### 1.1 UIToolkit 在 Unity 2022 中的地位

Unity 2022 LTS 是 UIToolkit 从 Editor-only 到**运行时正式生产可用**的关键版本节点。官方定位 UIToolkit 为长期替代 UGUI 的方案，底层字体渲染完全基于 **TextMeshPro SDF 技术**，但字体的引用方式、配置入口、回退机制与 UGUI 中使用 TMP 组件存在本质差异。

### 1.2 全球化字体面临的核心挑战

| 挑战维度 | 具体问题 |
|----------|----------|
| **字符集规模** | 中文 CJK 基本区 20,902 字符；全量 Unicode 14.0 超 144,697 字符 |
| **内存压力** | 全量中文 SDF Atlas 4096x4096 单张约 64MB（RGBA32），多张叠加可达数百 MB |
| **多语言混排** | 同屏可能同时出现 LTR（拉丁）/ RTL（阿拉伯/希伯来）/ Emoji |
| **运行时动态内容** | 玩家名、聊天、UGC 文字无法预知字符，静态字符集难以覆盖 |
| **包体控制** | 字体 .otf/.ttf 原文件 + SDF .asset 双份，CJK 字体原文件可达 20-40MB |
| **平台字体差异** | Windows/macOS/Android/iOS 系统字体路径与可用字体集完全不同 |

### 1.3 Unity 2022 UIToolkit 字体的关键限制

```css
/* 错误：USS 不支持 system-font() 函数 */
-unity-font-definition: system-font('Microsoft YaHei');

/* 错误：USS 不支持字体回退链 */
-unity-font-definition: url('a.asset'), url('b.asset');

/* 正确：USS 只支持单个 FontAsset 引用 */
-unity-font-definition: url("/Assets/Fonts/SourceHanSansSC-Bold SDF.asset");
-unity-font-definition: resource("Fonts/SourceHanSansSC-Bold SDF");
```

核心结论：
- `-unity-font-definition` 只接受单个 FontAsset 引用
- 多语言字体回退必须通过 `TextSettings.asset` 的 `Fallback Font Assets` 配置
- 系统字体回退需通过 C# 运行时动态注入，USS 无法直接引用系统字体

---

## 2. UIToolkit 字体系统架构

### 2.1 完整渲染链路

```
文字渲染请求（Label / TextField 等）
        |
        v
   USS 样式解析
   -unity-font-definition --> 主字体 FontAsset
        |
        v
   PanelSettings.asset
   └── textSettings --> TextSettings.asset
                |
                ├── m_DefaultFontAsset      <- 兜底默认字体
                ├── m_FallbackFontAssets[]  <- 全局回退链
                └── m_EmojiFallbackAssets[] <- Emoji 专用回退
        |
        v
   字符查找流程（按 Unicode Codepoint）
   [1] 主字体 FontAsset Character Table
   [2] 主字体 FontAsset 自身 Fallback List
   [3] TextSettings.m_FallbackFontAssets[] 顺序查找
   [4] TextSettings.m_DefaultFontAsset
   [5] 全部失败 --> 显示方块 Tofu
        |
        v
   SDF Atlas 采样 --> Shader 渲染 --> GPU 输出
```

### 2.2 资产依赖关系

```
UIDocument (GameObject)
    └── UIDocument 组件
            ├── Source Asset (.uxml)         <- 界面结构
            └── Panel Settings (.asset)      <- 渲染配置
                    ├── Theme Style Sheet    <- USS 样式
                    └── Text Settings        <- 字体配置入口
                            ├── Default Font Asset    (.asset)
                            ├── Fallback Font Assets  (.asset[])
                            └── Emoji Fallback Assets (.asset[])
```

### 2.3 FontAsset 内部结构

| 字段 | 说明 |
|------|------|
| Atlas Texture | SDF 贴图（可含多张），R8 或 RGBA32 格式 |
| Character Table | Unicode Codepoint 到 Glyph 的映射 |
| Glyph Table | 字形几何数据（UV、Bearing、Advance、Scale） |
| Kern Table | 字距调整对（Kerning Pairs） |
| Font Feature Table | OpenType 特性（连字 Ligature 等） |
| Fallback Font Assets | FontAsset 级别回退链（优先级高于 TextSettings 级别） |
| AtlasPopulationMode | Static（静态）或 Dynamic（动态）模式标志 |

---

## 3. SDF 字体原理深析

### 3.1 SDF 的本质

SDF（Signed Distance Field，有符号距离场）将字体轮廓转换为距离场贴图。每个像素存储到最近字体轮廓边缘的带符号距离（字形内部为正值，外部为负值，边缘为 0）。Shader 以阈值 0.5 进行 smoothstep 抗锯齿截断，在任意分辨率下产生清晰边缘。

| 维度 | 位图字体 | SDF 字体 |
|------|---------|----------|
| 缩放质量 | 放大锯齿明显 | 任意缩放无损 |
| 多尺寸支持 | 需要多套贴图 | 单张贴图多尺寸 |
| 描边/阴影 | 需预烘培 | Shader 实时计算 |
| 内存利用率 | 低（多套贴图） | 高 |
| CJK 适配 | 需超大贴图 | 动态扩容支持 |

### 3.2 Render Mode 选择

| Render Mode | 精度 | 适用场景 | 内存 |
|-------------|------|----------|------|
| SDF | 标准 8-bit | 常规 UI 文字（拉丁） | 低 |
| SDF8 | 8-bit 兼容 | 移动端拉丁文字 | 低 |
| SDF16 | 16-bit | 小号文字精细度要求高 | 中 |
| **SDF32** | **32-bit** | **CJK 复杂笔画（推荐）** | 中高 |
| HINTED_SDF | 含 Hinting | Windows 系统字体风格 | 低 |
| BITMAP | 位图 | 像素风格 UI | 极低 |

CJK 字体必须使用 SDF32：中文笔画细节丰富（撇、捺、钩等），SDF32 能更好保留笔画特征，避免小字号下笔画粘连或断裂。

### 3.3 Sampling Point Size 的影响

- 过小（如 24pt）：复杂汉字笔画细节丢失，放大后边缘模糊
- 过大（如 96pt）：精度极高但单张 Atlas 容纳字符数急剧减少
- CJK 推荐：`Auto Sizing` 或手动 `42-56pt`，配合 4096×4096 Atlas

### 3.4 Padding 与描边/阴影的关系

`Padding` 值决定每个字形在 Atlas 中的安全边距：

- 描边宽度上限 = Padding 值
- 阴影偏移绝对值上限 = Padding 值
- CJK 推荐 Padding = 9；需粗描边效果时可提高到 12

---

## 4. 字体资产创建详解

### 4.1 Font Asset Creator 参数速查

菜单：**Window → TextMeshPro → Font Asset Creator**

| 参数 | CJK 推荐 | 拉丁推荐 | 说明 |
|------|---------|---------|------|
| Source Font File | .otf/.ttf | .otf/.ttf | 源字体文件 |
| Sampling Point Size | Auto / 48pt | Auto / 36pt | 采样精度 |
| Padding | 9 | 6 | SDF 边缘扩展像素 |
| Packing Method | Optimum | Optimum | 字形排列算法 |
| Atlas Resolution | 4096×4096 | 2048×2048 | 单张 Atlas 尺寸 |
| Character Set | Custom Characters | ASCII+Extended | 字符集来源 |
| Render Mode | SDF32 | SDF32 | SDF 精度模式 |
| Get Kerning Pairs | 勾选 | 勾选 | 字距调整数据 |

### 4.2 字符集文件准备

```
GB2312（6,763 字）         -- 简体中文基础，覆盖日常用字
通用规范汉字表（8,105 字） -- 教育部标准，覆盖更广
GBK 全集（20,902 字）      -- 覆盖绝大多数简繁汉字
Unicode CJK 基本区         -- 20,902 字 + 各扩展块
```

生成中文字符集的 Python 脚本：

```python
# generate_charset.py
def generate_charset():
    chars = set()
    for i in range(32, 127):
        chars.add(chr(i))
    for high in range(0xB0, 0xF8):
        for low in range(0xA1, 0xFF):
            try:
                c = bytes([high, low]).decode('gb2312')
                chars.add(c)
            except Exception:
                pass
    extra = '，。！？；：「」【】《》“”‘’、…——～·℃％'
    chars.update(extra)
    return ''.join(sorted(chars))

if __name__ == '__main__':
    charset = generate_charset()
    with open('chinese_charset.txt', 'w', encoding='utf-8') as f:
        f.write(charset)
    print(f'共生成 {len(charset)} 个字符')
```

### 4.3 Multi-Atlas 自动扩容（Unity 2022）

Unity 2022 起，FontAsset 支持多张 Atlas 自动扩容（Multi-Atlas Textures）：

- 第一张 Atlas 装满后自动创建第二张、第三张
- 对 Dynamic 模式尤其关键，防止 Atlas 满后丢字
- Inspector 中可见 `Atlas Textures` 数组
- 每张额外 Atlas 独立占用内存，需纳入预算规划

---

## 5. 动态字体 vs 静态字体决策

### 5.1 两种模式核心对比

| 维度 | Static 静态 | Dynamic 动态 |
|------|------------|-------------|
| 字符集 | 编辑时固定 | 运行时按需生成 |
| 首帧性能 | 无开销 | 遇新字符触发 Atlas 更新 |
| 内存可控性 | 精确可控 | 依赖运行时字符量 |
| 包体影响 | Atlas 随包打包（较大）| 运行时生成，包内无贴图 |
| 未知字符处理 | 显示方块 Tofu | 自动生成并缓存 |
| 适用场景 | 固定文案、本地化文本 | 玩家名、聊天、UGC |

### 5.2 决策流程

```
字符集是否完全可预知？
    ├── 是 → Static 模式：提前生成完整字符集 Atlas 打包进游戏
    └── 否 → Dynamic 模式
            ├── 有网络？ → Dynamic + AssetBundle 按需下载
            ├── 无网络？ → Dynamic + 系统字体回退（见第8节）
            └── 移动端内存紧张？ → Dynamic + 字符预热 + Atlas 上限控制
```

### 5.3 Dynamic 模式三大陷阱

**陷阱1：Atlas 更新触发当帧卡顿**  
动态添加新字符时 Unity 重建 Atlas 并重新上传 GPU，中低端机可达 16ms+ 卡顿。  
解决：场景加载时预热所有可能出现的字符集（见第13.3节）。

**陷阱2：Dynamic FontAsset 不能跨 AssetBundle 共享**  
Dynamic FontAsset 运行时修改自身 Atlas Texture，导致 AB 内其他资源对该贴图的引用失效。  
解决：Dynamic FontAsset 放 Resources 或单独 AB，不与其他资源混包。

**陷阱3：Editor Play Mode 下动态字符被重置**  
进入 Play Mode 时 Dynamic Atlas 被清空重建（Editor 沙盒机制）。生产构建无此问题。

---

## 6. 中文字体集成实战

### 6.1 推荐字体选型

| 字体 | 授权 | 覆盖范围 | 特点 |
|------|------|---------|------|
| **思源黑体 SC** | OFL | 简繁日韩 CJK | Adobe+Google 联合出品，质量最高 |
| **思源宋体 SC** | OFL | 简繁日韩 CJK | 宋体风格，适合阅读类 UI |
| **Noto Sans SC** | OFL | 简体中文 | 与思源黑体同源，Google 出品 |
| **Noto Sans CJK** | OFL | 完整 CJK | 涵盖简繁日韩，单文件体积最大 |
| 微软雅黑 | 商业授权 | 简体中文 | 仅限系统自带，**不可随包分发** |

### 6.2 完整集成六步骤

**Step 1：导入字体原文件**
```
将 SourceHanSansSC-Bold.otf 放入 Assets/Fonts/
Unity 自动生成 .meta，Font 导入设置保持默认
```

**Step 2：生成 SDF FontAsset**
```
菜单：Window → TextMeshPro → Font Asset Creator
  Source Font File:    SourceHanSansSC-Bold.otf
  Sampling Point Size: Auto Sizing
  Padding:             9
  Atlas Resolution:    4096 × 4096
  Character Set:       Custom Characters（粘贴字符集文件内容）
  Render Mode:         SDF32
  Get Kerning Pairs:   勾选

[Generate Font Atlas] → 等待生成 → [Save]
保存为：Assets/Fonts/SourceHanSansSC-Bold SDF.asset
```

**Step 3：配置 TextSettings**
```
Assets/TextSettings.asset（Inspector）
  Default Font Asset → 拖入 SourceHanSansSC-Bold SDF.asset
  Fallback Font Assets → 按需添加多语言字体（详见第7节）
```

**Step 4：确认 PanelSettings 引用**
```
Assets/Resources/PanelSettings.asset（Inspector）
  Text Settings → 确认已指向 Assets/TextSettings.asset
```

**Step 5：USS 引用字体**
```css
:root {
    -unity-font-definition: url("/Assets/Fonts/SourceHanSansSC-Bold SDF.asset");
    font-size: 16px;
}
```

**Step 6：运行验证**
```
Play Mode → Label 中显示中文 → 检查是否正常（无方块）
若有方块：打开 FontAsset Inspector → Character Table → 搜索该字符
不存在则：扩大字符集重新生成 Atlas，或将该字符所属字体加入 Fallback
```

### 6.3 思源黑体 CJK 覆盖范围

| 字符类别 | 覆盖情况 |
|---------|----------|
| 简体中文常用字 | 完整（GB2312 全集）|
| 繁体中文 | 完整 |
| 日文汉字 + 假名 | 完整 |
| 韩文汉字 + 谚文 | 完整 |
| 基本拉丁字母 | 完整 |
| 泰文 | 不覆盖（需 Noto Sans Thai）|
| 阿拉伯文 | 不覆盖（需 Noto Sans Arabic）|
| Emoji | 不覆盖（需独立 Emoji FontAsset）|

---

## 7. 字体回退系统 Font Fallback

### 7.1 回退层级与优先级

```
[层级 1] FontAsset 自身 Fallback（最高优先级）
         FontAsset Inspector → Fallback Font Assets
         适用：某个特定字体补充特定字符集

[层级 2] TextSettings 全局 Fallback
         TextSettings.asset → Fallback Font Assets
         适用：全局兜底，所有字体共享的回退链

[层级 3] TextSettings Default Font Asset（最低优先级）
         TextSettings.asset → Default Font Asset
         适用：最终兜底，确保不显示方块
```

### 7.2 Inspector 配置全局 Fallback

```
1. Project 窗口选中 Assets/TextSettings.asset
2. Inspector → Fallback Font Assets → 点击 + 按如下顺序添加：

   [0] NotoSansSC-Regular SDF       ← 简体中文补充
   [1] NotoSansTC-Regular SDF       ← 繁体中文
   [2] NotoSansJP-Regular SDF       ← 日文
   [3] NotoSansKR-Regular SDF       ← 韩文
   [4] NotoSansThai-Regular SDF     ← 泰文
   [5] NotoSansArabic-Regular SDF   ← 阿拉伯文（RTL）
   [6] NotoEmoji-Regular SDF        ← Emoji
```

### 7.3 C# 代码动态配置 Fallback

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class FontFallbackManager : MonoBehaviour
{
    void Awake()
    {
        InjectFallbackByLanguage();
    }

    private void InjectFallbackByLanguage()
    {
        string lang = System.Globalization.CultureInfo
            .CurrentCulture.TwoLetterISOLanguageName;

        // 加载主字体
        var primary = Resources.Load<FontAsset>("Fonts/SourceHanSansSC-Bold SDF");
        if (primary == null) return;

        // 按语言注入回退
        string fallbackPath = lang switch
        {
            "zh" => "Fonts/NotoSansSC-Regular SDF",
            "ja" => "Fonts/NotoSansJP-Regular SDF",
            "ko" => "Fonts/NotoSansKR-Regular SDF",
            "ar" => "Fonts/NotoSansArabic-Regular SDF",
            "th" => "Fonts/NotoSansThai-Regular SDF",
            _    => "Fonts/NotoSans-Regular SDF",
        };

        var fallback = Resources.Load<FontAsset>(fallbackPath);
        if (fallback != null && !primary.fallbackFontAssetTable.Contains(fallback))
        {
            primary.fallbackFontAssetTable.Add(fallback);
        }
    }
}
```

### 7.4 Emoji 字体回退

Unity 2022 对 Emoji 提供专项支持：

```
推荐 Emoji 字体：
  Noto Emoji（Google，OFL 授权）
  Twemoji（Twitter，CC BY 4.0）

配置位置：TextSettings.asset → Emoji Fallback Font Assets（独立列表）
注意：Emoji 字体使用 Color 模式（RGBA32），不是 SDF 灰度模式
```

---

## 8. 系统字体自动回退

### 8.1 为什么需要系统字体回退

以下场景无法预知用户输入的字符：
- 玩家自定义名称（可能含生僻字、各国文字）
- 多语言聊天系统
- 用户生成内容（UGC）
- 来自操作系统的通知文本

将所有 Unicode 字符预烘培为 SDF 不现实（内存和包体不可接受）。需要运行时读取系统字体作为最终兜底。

### 8.2 各平台系统字体路径

```csharp
public static class SystemFontHelper
{
    public static string[] GetCandidatePaths()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return new[]
        {
            @"C:\Windows\Fonts\msyh.ttc",    // 微软雅黑（简体中文）
            @"C:\Windows\Fonts\msjh.ttc",    // 微软正黑（繁体中文）
            @"C:\Windows\Fonts\msgothic.ttc", // MS Gothic（日文）
            @"C:\Windows\Fonts\malgun.ttf",   // 맑은 고딕（韩文）
            @"C:\Windows\Fonts\arial.ttf",    // Arial（拉丁兜底）
        };
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        return new[]
        {
            "/System/Library/Fonts/PingFang.ttc",           // 苹方（简繁中文）
            "/System/Library/Fonts/Hiragino Sans GB.ttc",   // 冬青黑体（简体）
            "/Library/Fonts/Arial Unicode.ttf",             // Arial Unicode
            "/System/Library/Fonts/Helvetica.ttc",          // Helvetica（拉丁）
        };
#elif UNITY_ANDROID
        return new[]
        {
            "/system/fonts/NotoSansCJK-Regular.ttc", // AOSP 默认 CJK
            "/system/fonts/DroidSansFallback.ttf",   // 旧版 Android CJK
            "/system/fonts/Roboto-Regular.ttf",      // 拉丁兜底
        };
#elif UNITY_IOS
        return new[]
        {
            "/System/Library/Fonts/PingFang.ttc",    // 苹方
            "/System/Library/Fonts/Helvetica.ttc",   // Helvetica
        };
#else
        return System.Array.Empty<string>();
#endif
    }
}
```

### 8.3 运行时从系统字体生成 FontAsset

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using System.IO;

public class SystemFontFallback : MonoBehaviour
{
    private static FontAsset _systemFallback;

    public static FontAsset GetOrCreate()
    {
        if (_systemFallback != null) return _systemFallback;

        foreach (var path in SystemFontHelper.GetCandidatePaths())
        {
            if (!File.Exists(path)) continue;

            // 从系统字体文件创建 Font 对象
            var font = new Font(path);
            if (font == null) continue;

            // 创建 Dynamic FontAsset（不预烘培字符集）
            _systemFallback = FontAsset.CreateFontAsset(
                font,
                samplingPointSize: 36,
                atlasPadding: 6,
                renderMode: GlyphRenderMode.SDF32,
                atlasWidth: 2048,
                atlasHeight: 2048,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true
            );

            if (_systemFallback != null)
            {
                Debug.Log($"[SystemFont] 已从系统字体创建回退: {path}");
                return _systemFallback;
            }
        }

        Debug.LogWarning("[SystemFont] 未找到任何可用系统字体");
        return null;
    }

    void Start()
    {
        var systemFont = GetOrCreate();
        if (systemFont == null) return;

        // 注入到主字体的 Fallback 链末尾
        var primary = Resources.Load<FontAsset>("Fonts/SourceHanSansSC-Bold SDF");
        if (primary != null && !primary.fallbackFontAssetTable.Contains(systemFont))
        {
            primary.fallbackFontAssetTable.Add(systemFont);
        }
    }
}
```

### 8.4 系统字体回退的局限性

| 限制 | 说明 |
|------|------|
| 平台授权 | 系统字体受平台授权约束，不可提取用于其他用途 |
| 字体质量不一致 | 不同 Android 设备内置字体质量差异大 |
| 沙盒限制 | iOS 沙盒环境下字体路径可能不稳定 |
| 首次渲染开销 | Dynamic 模式首次遇到字符需重建 Atlas |
| 无法保证存在 | 低版本 Android 可能不含 NotoSansCJK |

---

## 9. 全球化多语言方案设计

### 9.1 语言分组与字体映射策略

将所有目标语言按字体需求分组，共享字体资产：

| 语言组 | 语言 | 推荐字体 | Atlas 尺寸 |
|--------|------|---------|------------|
| **CJK** | 简体中文、繁体中文、日文、韩文 | Source Han Sans SC | 4096×4096 |
| **拉丁** | 英语、法语、德语、西班牙语等 | Noto Sans | 2048×2048 |
| **阿拉伯** | 阿拉伯语、波斯语、乌尔都语 | Noto Sans Arabic | 2048×2048 |
| **泰文** | 泰语 | Noto Sans Thai | 2048×2048 |
| **其他** | 希伯来语、印地语、越南语等 | Noto Sans 对应子集 | 2048×2048 |

### 9.2 本地化字体切换架构

```csharp
using UnityEngine;
using UnityEngine.TextCore.Text;
using TMPro;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LocaleFontConfig", menuName = "UI/Locale Font Config")]
public class LocaleFontConfig : ScriptableObject
{
    [System.Serializable]
    public class LocaleEntry
    {
        public string localeCode;          // "zh-CN", "ja", "ar" 等
        public FontAsset primaryFont;      // 主字体
        public FontAsset[] fallbackFonts;  // 该语言专属回退链
    }

    public LocaleEntry[] entries;
    public FontAsset defaultFont;          // 兜底字体

    public LocaleEntry FindEntry(string localeCode)
    {
        // 精确匹配
        foreach (var e in entries)
            if (e.localeCode == localeCode) return e;
        // 语言码匹配（zh-CN → zh）
        string lang = localeCode.Split('-')[0];
        foreach (var e in entries)
            if (e.localeCode.StartsWith(lang)) return e;
        return null;
    }
}

public class LocaleFontSwitcher : MonoBehaviour
{
    [SerializeField] private LocaleFontConfig config;
    [SerializeField] private UnityEngine.UIElements.PanelSettings panelSettings;

    public void ApplyLocale(string localeCode)
    {
        var entry = config.FindEntry(localeCode);
        var primaryFont = entry?.primaryFont ?? config.defaultFont;
        if (primaryFont == null) return;

        // 重建回退链
        primaryFont.fallbackFontAssetTable.Clear();
        if (entry?.fallbackFonts != null)
        {
            foreach (var f in entry.fallbackFonts)
                if (f != null) primaryFont.fallbackFontAssetTable.Add(f);
        }

        Debug.Log($"[LocaleFont] 已切换语言字体: {localeCode} → {primaryFont.name}");
    }
}
```

### 9.3 RTL（从右到左）语言支持

Unity 2022 UIToolkit 对 RTL 的支持需要额外配置：

```csharp
// 对包含阿拉伯文/希伯来文的 Label 设置方向
var label = root.Q<Label>("arabic-label");
label.style.unityTextAlign = TextAnchor.MiddleRight; // 右对齐

// UIToolkit 目前（2022）不支持原生 BiDi 自动检测
// 需要使用第三方库（如 RTL-TMPro）或手动处理文本方向
// 或等待 Unity 2023+ 的 BiDi 原生支持
```

**RTL 临时解决方案**：
1. 使用 Unity Localization Package 管理文本，配合 RTL 预处理器
2. 引入 `RTLTMPro` 插件（开源，支持 UIToolkit Label）
3. 对阿拉伯文、希伯来文单独使用专用 Label 组件

### 9.4 AssetBundle 分包策略

```
字体 AssetBundle 推荐分组：

  fonts-latin.bundle    -- 拉丁字体（~2MB）     随包内置
  fonts-cjk.bundle      -- CJK 字体（~30-80MB） 按需下载
  fonts-arabic.bundle   -- 阿拉伯字体（~5MB）   按需下载
  fonts-thai.bundle     -- 泰文字体（~3MB）      按需下载
  fonts-emoji.bundle    -- Emoji 字体（~10MB）   按需下载

下载时机：
  - 检测系统语言 → 预下载对应语言包
  - 玩家选择语言 → 下载对应字体包
  - 首次遇到未支持字符 → 静默后台下载
```

---

## 10. USS 样式中的字体声明

### 10.1 字体相关 USS 属性完整列表

```css
.text-element {
    /* 字体资产引用（只支持单个 FontAsset）*/
    -unity-font-definition: url("/Assets/Fonts/SourceHanSansSC-Bold SDF.asset");

    /* 也可用传统 Font 对象（非 SDF，画质差，不推荐）*/
    /* -unity-font: url("/Assets/Fonts/SourceHanSansSC-Bold.otf"); */

    /* 字体大小 */
    font-size: 16px;

    /* 字体样式 */
    -unity-font-style: bold;          /* normal | bold | italic | bold-and-italic */

    /* 文字颜色 */
    color: rgba(255, 255, 255, 1);

    /* 文字对齐 */
    -unity-text-align: middle-center; /* upper-left | middle-center | lower-right 等 */

    /* 文字溢出处理 */
    overflow: hidden;
    text-overflow: ellipsis;  /* 超出显示省略号（需配合 overflow: hidden）*/

    /* 单行/多行 */
    white-space: nowrap;   /* 单行：nowrap | 多行：normal */

    /* 字间距（像素）*/
    letter-spacing: 1px;

    /* 行间距（相对单位）*/
    -unity-paragraph-spacing: 4px;

    /* 描边（SDF Shader 支持）*/
    -unity-text-outline-width: 1px;
    -unity-text-outline-color: rgba(0, 0, 0, 0.8);
}
```

### 10.2 url() vs resource() 的选择

| 方式 | 语法 | 路径基准 | 是否需要扩展名 | 适用场景 |
|------|------|---------|--------------|----------|
| `url()` | `url("/Assets/Fonts/X SDF.asset")` | 项目根目录 | 需要 `.asset` | 通用，路径明确 |
| `resource()` | `resource("Fonts/X SDF")` | Assets/Resources/ | 不需要 | 字体在 Resources 下时 |

注意 `url()` 路径中的**空格**：Unity FontAsset 默认命名含空格（如 `SourceHanSansSC-Bold SDF.asset`），路径中空格必须保留原样，不能用 `%20` 替换。

### 10.3 USS 继承与覆盖规则

```css
/* 根级定义全局字体 */
:root {
    -unity-font-definition: url("/Assets/Fonts/SourceHanSansSC-Bold SDF.asset");
    font-size: 16px;
    color: #e2e8f0;
}

/* 子元素继承，可单独覆盖字号但继承字体 */
.title {
    font-size: 24px;
    -unity-font-style: bold;
}

/* 完全覆盖字体（使用不同 FontAsset）*/
.monospace-text {
    -unity-font-definition: resource("Fonts/JetBrainsMono-Regular SDF");
    font-size: 13px;
}
```

---

## 11. UXML 与 C# 字体绑定

### 11.1 UXML 中的字体引用

UXML 本身不直接引用字体，字体通过 USS 样式表控制。UXML 只负责结构：

```xml
<?xml version="1.0" encoding="utf-8"?>
<engine:UXML
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xmlns:engine="UnityEngine.UIElements"
    xsi:noNamespaceSchemaLocation="../UIElementsSchema/UIElements.xsd">

    <!-- 引用包含字体定义的样式表 -->
    <engine:Style src="/Assets/UI/Styles/GlobalFont.uss" />
    <engine:Style src="/Assets/UI/Styles/GateTheme.uss" />

    <engine:Label class="title" text="全球化字体示例" />
    <engine:Label class="body-text" text="Hello 世界 こんにちは" />
    <engine:Label class="arabic-text" text="مرحبا بالعالم" />
</engine:UXML>
```

### 11.2 C# 代码动态设置字体

```csharp
using UnityEngine;
using UnityEngine.UIElements;
using TMPro;
using UnityEngine.TextCore.Text;

public class FontBinder : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private FontAsset boldFont;
    [SerializeField] private FontAsset lightFont;

    void Start()
    {
        var root = uiDocument.rootVisualElement;

        // 方式1：通过 style 属性直接设置（运行时生效）
        var titleLabel = root.Q<Label>("title-label");
        if (titleLabel != null)
        {
            titleLabel.style.unityFontDefinition =
                new StyleFontDefinition(boldFont);
            titleLabel.style.fontSize = 24;
        }

        // 方式2：遍历所有同类元素批量设置
        root.Query<Label>(className: "body-text").ForEach(label =>
        {
            label.style.unityFontDefinition =
                new StyleFontDefinition(lightFont);
        });

        // 方式3：通过 USS 类切换（推荐，保持样式与逻辑分离）
        var arabicLabel = root.Q<Label>("arabic-label");
        arabicLabel?.AddToClassList("rtl-text");
    }
}
```

### 11.3 运行时动态修改 USS 变量

```csharp
// 通过 CustomStyleProperty 动态注入字体（USS 变量方案）
// USS 中定义：
// :root { --main-font: url("/Assets/Fonts/A SDF.asset"); }
// .text { -unity-font-definition: var(--main-font); }

// C# 中修改根元素自定义属性（Unity 2022 支持）
var root = uiDocument.rootVisualElement;
root.styleSheets.Add(newStyleSheet); // 切换整张样式表
```

---

## 12. 运行时动态字体加载

### 12.1 从 Resources 同步加载

```csharp
// 适合字体体积小、不影响加载时间的场景
var fontAsset = Resources.Load<FontAsset>("Fonts/NotoSansJP-Regular SDF");
if (fontAsset != null)
{
    var label = root.Q<Label>("jp-label");
    label.style.unityFontDefinition = new StyleFontDefinition(fontAsset);
}
```

### 12.2 从 AssetBundle 异步加载

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;

public class FontBundleLoader : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private const string BundleUrl =
        "https://cdn.example.com/bundles/fonts-cjk.bundle";

    IEnumerator LoadCJKFont()
    {
        // 1. 下载 AssetBundle
        var req = UnityEngine.Networking.UnityWebRequestAssetBundle
            .GetAssetBundle(BundleUrl);
        yield return req.SendWebRequest();

        if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[FontLoader] 下载失败: {req.error}");
            yield break;
        }

        // 2. 获取 Bundle
        var bundle = UnityEngine.Networking.DownloadHandlerAssetBundle
            .GetContent(req);

        // 3. 加载 FontAsset
        var fontReq = bundle.LoadAssetAsync<FontAsset>(
            "NotoSansSC-Regular SDF");
        yield return fontReq;

        var fontAsset = fontReq.asset as FontAsset;
        if (fontAsset == null)
        {
            Debug.LogError("[FontLoader] FontAsset 加载失败");
            yield break;
        }

        // 4. 注入到主字体 Fallback 链
        var primary = Resources.Load<FontAsset>(
            "Fonts/SourceHanSansSC-Bold SDF");
        if (primary != null &&
            !primary.fallbackFontAssetTable.Contains(fontAsset))
        {
            primary.fallbackFontAssetTable.Add(fontAsset);
            Debug.Log("[FontLoader] CJK 字体已注入 Fallback 链");
        }

        // 5. 释放 Bundle（但保留已加载的 Asset）
        bundle.Unload(false);
    }

    void Start() => StartCoroutine(LoadCJKFont());
}
```

### 12.3 Addressables 异步加载（推荐生产环境）

```csharp
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TextCore.Text;

public class AddressableFontLoader : MonoBehaviour
{
    [SerializeField] private AssetReferenceT<FontAsset> cjkFontRef;

    async void Start()
    {
        var handle = cjkFontRef.LoadAssetAsync<FontAsset>();
        await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("[FontLoader] Addressable 字体加载失败");
            return;
        }

        var fontAsset = handle.Result;
        var primary = Resources.Load<FontAsset>(
            "Fonts/SourceHanSansSC-Bold SDF");
        primary?.fallbackFontAssetTable.Add(fontAsset);
    }
}
```

---

## 13. 性能优化深度策略

### 13.1 Atlas 尺寸规划

| 字符集规模 | 推荐 Atlas 尺寸 | 显存占用（R8）| 显存占用（RGBA32）|
|-----------|--------------|-------------|------------------|
| ASCII+标点（~200字）| 512×512 | 0.25MB | 1MB |
| 拉丁扩展（~1,000字）| 1024×1024 | 1MB | 4MB |
| GB2312（~7,000字）| 4096×4096 | 16MB | 64MB |
| GBK 全集（~21,000字）| 4096×4096 ×3张 | 48MB | 192MB |

**结论**：
- CJK 字体使用 `R8`（灰度）格式而非 `RGBA32`，节省 75% 显存
- 在 Font Asset Creator 中选择 `Atlas Texture Format: R8` 可大幅降低内存

### 13.2 字符集精简策略

```csharp
// 统计项目中实际使用的汉字
// 工具：用脚本扫描所有本地化文件，提取唯一字符集

using System.Collections.Generic;
using System.IO;
using System.Text;

public static class CharsetAnalyzer
{
    public static void AnalyzeLocalizationFiles(string locaFolder)
    {
        var usedChars = new HashSet<char>();

        foreach (var file in Directory.GetFiles(
            locaFolder, "*.json", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file, Encoding.UTF8);
            foreach (var c in text)
            {
                if (c > '\u4E00' && c < '\u9FFF') // CJK 基本区
                    usedChars.Add(c);
            }
        }

        var result = new string(new List<char>(usedChars).ToArray());
        File.WriteAllText("used_chars.txt", result, Encoding.UTF8);
        Debug.Log($"共统计到 {usedChars.Count} 个唯一汉字");
    }
}
```

### 13.3 字符预热（Warm-Up）

避免运行时 Dynamic Atlas 更新造成卡顿：

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class FontWarmup : MonoBehaviour
{
    [SerializeField] private FontAsset fontAsset;
    [TextArea(3, 10)]
    [SerializeField] private string warmupCharacters;

    // 在场景加载完成后、UI 显示前调用
    public void DoWarmup()
    {
        if (fontAsset == null || string.IsNullOrEmpty(warmupCharacters))
            return;

        // Unity 2022: FontAsset.HasCharacters 触发动态字符生成
        fontAsset.HasCharacters(warmupCharacters, out _, true, true);
        Debug.Log($"[FontWarmup] 预热完成，共 {warmupCharacters.Length} 个字符");
    }

    IEnumerator Start()
    {
        // 等一帧确保 UI 系统初始化完成
        yield return null;
        DoWarmup();
    }
}
```

### 13.4 DrawCall 与批处理优化

```
优化原则：

1. 同一 Panel 内使用相同 FontAsset 的文字会被批处理
   → 尽量让同屏文字使用同一 FontAsset

2. 不同 FontAsset 的文字无法合批，每种字体产生独立 DrawCall
   → Fallback 字体被触发时会打断批处理

3. 描边、阴影、发光属性相同的文字可合批
   → 避免每个 Label 使用不同的描边参数

4. Dynamic Atlas 更新会导致当帧所有文字 DrawCall 重建
   → 场景加载阶段完成字符预热，避免游戏运行时触发

5. 使用 Frame Debugger（Window → Analysis → Frame Debugger）
   观察 UI.Render 下的 DrawCall 数量
```

### 13.5 内存管理

```csharp
// 卸载不再需要的语言字体（切换语言后）
public void UnloadUnusedFonts(FontAsset[] fontsToUnload)
{
    foreach (var font in fontsToUnload)
    {
        if (font == null) continue;
        // 从 Fallback 链中移除
        var primary = Resources.Load<FontAsset>("Fonts/SourceHanSansSC-Bold SDF");
        primary?.fallbackFontAssetTable.Remove(font);
        // 卸载 Atlas 贴图释放显存
        Resources.UnloadAsset(font);
    }
}
```

---

## 14. 常见问题排查手册

### 14.1 文字显示方块 □（Tofu）

```
排查步骤：

1. 确认 FontAsset 是否包含该字符
   → Project 窗口选中 FontAsset → Inspector → Character Table → 搜索字符

2. 确认 TextSettings.asset 是否被 PanelSettings 引用
   → PanelSettings Inspector → Text Settings 字段是否非空

3. 确认 Fallback 链是否配置正确
   → TextSettings Inspector → Fallback Font Assets 列表

4. 若为 Dynamic 模式，确认 AtlasPopulationMode 为 Dynamic
   → FontAsset Inspector → Atlas Population Mode

5. 检查是否有多个 PanelSettings，不同 Panel 使用了不同 TextSettings
   → 场景中所有 UIDocument 组件检查 PanelSettings 引用
```

### 14.2 USS 字体设置不生效

```
排查步骤：

1. 检查 url() 路径是否正确
   → 路径区分大小写，空格必须保留，以 / 开头从项目根目录开始
   → 正确示例：url("/Assets/Fonts/SourceHanSansSC-Bold SDF.asset")

2. 检查 StyleSheet 是否被正确引用
   → UIDocument Inspector → Panel Settings → Theme Style Sheet
   → 或 UXML 内 <Style src="..."> 标签

3. 检查是否有更高优先级样式覆盖
   → 使用 UI Debugger（Window → UI Toolkit → Debugger）
   → 选中元素查看 Matched Rules，找到覆盖来源

4. 检查 .asset 扩展名是否遗漏
   → resource() 不需要扩展名；url() 必须包含 .asset
```

### 14.3 中文字体生成时间过长

```
优化生成速度：

1. 减少字符集规模（从 GBK 全集缩减到 GB2312）
   → 用 CharsetAnalyzer 扫描实际用到的字符（见13.2节）

2. 降低 Atlas Resolution（临时用 2048×2048 快速验证效果）

3. 降低 Sampling Point Size（从 48pt 降到 36pt）

4. 在性能好的开发机上生成，生成好的 .asset 提交到版本库
   → 其他开发者无需重新生成

5. 使用 Dynamic 模式代替完整静态字符集
   → 只预热高频字符，其余按需生成
```

### 14.4 字体在移动端显示模糊

```
排查与解决：

1. 检查 Render Mode 是否为 SDF32
   → FontAsset Inspector → Render Mode

2. 检查 Sampling Point Size 是否过小
   → 建议不低于 36pt，CJK 建议 42pt+

3. 检查设备 DPI 与 UI Scale 设置
   → PanelSettings → Scale Mode → Scale With Screen Size
   → Reference Resolution 设置为目标分辨率（如 1920×1080）

4. 检查 Canvas 缩放导致的次像素渲染
   → UIToolkit 不存在 UGUI 的 Canvas Scaler 问题，但注意
      PanelSettings 的 DPI 缩放设置

5. SDF 字体在极小字号（< 10px）下会模糊，这是 SDF 的固有限制
   → 极小文字改用 BITMAP 模式 FontAsset
```

### 14.5 Font Asset Creator 菜单不存在

```
解决方案：

1. 确认已安装 TextMeshPro 包
   → Window → Package Manager → 搜索 TextMeshPro → Install

2. Unity 2022 中 UIToolkit 使用独立 TextSettings，
   而非 TMP 的 TMP_Settings，两者互相独立

3. 若 Window → TextMeshPro 菜单存在但 Font Asset Creator 灰色
   → 确认 Project 窗口中有字体文件被选中
```

### 14.6 Editor 与构建版本字体不一致

```
常见原因：

1. Dynamic FontAsset 在 Editor 中被重置（见5.3节陷阱3）
   → 在构建版本中测试，Editor 中的表现仅供参考

2. 系统字体回退在不同机器上使用了不同字体
   → 使用打包的 FontAsset 而非系统字体作为主字体

3. AssetBundle 字体版本与项目版本不匹配
   → 字体 AB 与游戏客户端版本绑定，同步更新
```

### 14.7 RTL 阿拉伯文显示乱序

```
原因：Unity 2022 UIToolkit 不支持原生 BiDi 双向文字算法

解决方案（三选一）：

1. 使用 RTLTMPro 开源库（支持 UIToolkit）
   → https://github.com/pnarimani/RTLTMPro

2. 使用 Unity Localization Package 配合 RTL 预处理器
   → 在存储阶段就反转阿拉伯文字符串

3. 等待 Unity 2023+ 的原生 BiDi 支持
   → Unity 官方路线图已列入
```

---

## 15. 完整项目结构参考

### 15.1 推荐目录结构

```
Assets/
├── Fonts/
│   ├── Source/                          # 原始字体文件（.otf/.ttf）
│   │   ├── SourceHanSansSC-Bold.otf     # 思源黑体（CJK 主字体）
│   │   ├── NotoSans-Regular.otf         # Noto Sans（拉丁回退）
│   │   ├── NotoSansArabic-Regular.otf   # 阿拉伯文回退
│   │   ├── NotoSansThai-Regular.otf     # 泰文回退
│   │   └── NotoEmoji-Regular.ttf        # Emoji 回退
│   └── SDF/                             # 生成的 FontAsset（.asset）
│       ├── SourceHanSansSC-Bold SDF.asset
│       ├── NotoSans-Regular SDF.asset
│       ├── NotoSansArabic-Regular SDF.asset
│       ├── NotoSansThai-Regular SDF.asset
│       └── NotoEmoji-Regular SDF.asset
│
├── Resources/
│   ├── Fonts/                           # 运行时动态加载的字体
│   │   └── SourceHanSansSC-Bold SDF.asset
│   └── PanelSettings.asset             # UI 面板设置
│
├── UI/
│   ├── Styles/
│   │   ├── GlobalFont.uss              # 全局字体定义
│   │   ├── GateTheme.uss               # 主题样式
│   │   └── RTL.uss                     # RTL 语言专用样式
│   ├── UXML/                           # 界面布局文件
│   └── Scripts/
│       ├── FontFallbackManager.cs      # 全局回退管理
│       ├── LocaleFontSwitcher.cs       # 多语言字体切换
│       ├── FontBundleLoader.cs         # AB 异步加载
│       ├── FontWarmup.cs               # 字符预热
│       └── SystemFontFallback.cs       # 系统字体回退
│
├── TextSettings.asset                  # UIToolkit 文字设置（核心）
└── StreamingAssets/
    └── Fonts/                          # 可选：大体积字体本地缓存
        └── fonts-cjk.bundle
```

### 15.2 GlobalFont.uss 完整参考

```css
/*
 * GlobalFont.uss
 * 全局字体定义
 * 多语言回退通过 TextSettings.asset → Fallback Font Assets 配置
 * USS 本身只定义主字体，不支持回退链语法
 */

:root {
    -unity-font-definition: url("/Assets/Fonts/SDF/SourceHanSansSC-Bold SDF.asset");
    font-size: 16px;
    color: #e2e8f0;
    -unity-text-align: middle-left;
    white-space: normal;
}

/* 标题 */
.text-title {
    font-size: 24px;
    -unity-font-style: bold;
    color: #f8fafc;
    letter-spacing: 0.5px;
}

/* 正文 */
.text-body {
    font-size: 15px;
    -unity-font-style: normal;
    color: #cbd5e1;
}

/* 小字提示 */
.text-caption {
    font-size: 12px;
    color: #64748b;
}

/* 代码/等宽 */
.text-mono {
    -unity-font-definition: resource("Fonts/JetBrainsMono-Regular SDF");
    font-size: 13px;
    color: #a5f3fc;
}

/* RTL 语言容器 */
.rtl-container {
    -unity-text-align: middle-right;
    flex-direction: row-reverse;
}
```

### 15.3 TextSettings.asset 关键字段速查

通过 Inspector 配置，无需手动编辑 YAML。关键字段说明：

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_DefaultFontAsset` | FontAsset | 全局默认字体，所有未指定字体的元素使用此字体 |
| `m_FallbackFontAssets` | FontAsset[] | 全局回退链，字符查找失败后按顺序遍历 |
| `m_DefaultFontSize` | float | 全局默认字号（被 USS font-size 覆盖）|
| `m_EmojiFallbackTextAssets` | FontAsset[] | Emoji 专用回退列表 |
| `m_MissingGlyphCharacter` | uint | 找不到字符时显示的替代字符 Unicode 码点 |

### 15.4 字体配置检查清单

```
部署前字体配置验证清单：

[ ] FontAsset 使用 SDF32 Render Mode（CJK 字体）
[ ] Atlas Resolution >= 4096×4096（CJK 字体）
[ ] Padding >= 9（CJK 字体）
[ ] Atlas Texture Format 设为 R8（灰度，节省 75% 显存）
[ ] TextSettings.asset 已被 PanelSettings.asset 引用
[ ] Fallback Font Assets 列表按语言优先级排列
[ ] Dynamic FontAsset 未与其他资源混打 AssetBundle
[ ] 场景加载时已执行字符预热（FontWarmup）
[ ] RTL 语言已配置右对齐样式
[ ] Emoji FontAsset 使用 Color/RGBA 模式（非 SDF 灰度）
[ ] 已用 Frame Debugger 验证同字体文字可合批
[ ] 字体原文件授权已确认（OFL 可商用）
```

### 15.5 关键 API 速查表

| 操作 | API | 命名空间 |
|------|-----|----------|
| 加载 FontAsset | `Resources.Load<FontAsset>(path)` | UnityEngine |
| 动态设置字体 | `label.style.unityFontDefinition = new StyleFontDefinition(fa)` | UnityEngine.UIElements |
| 添加 Fallback | `fontAsset.fallbackFontAssetTable.Add(fa)` | UnityEngine.TextCore.Text |
| 字符预热 | `fontAsset.HasCharacters(str, out _, true, true)` | UnityEngine.TextCore.Text |
| 创建动态字体 | `FontAsset.CreateFontAsset(font, ...)` | UnityEngine.TextCore.Text |
| 查询字符存在 | `fontAsset.HasCharacter(unicode)` | UnityEngine.TextCore.Text |
| 获取 Atlas 贴图 | `fontAsset.atlasTextures` | UnityEngine.TextCore.Text |

---

## 参考资料

- [Unity 2022 UIToolkit 官方文档 - Font Assets](https://docs.unity3d.com/2022.3/Documentation/Manual/UIE-font-asset.html)
- [Unity 2022 TextCore 官方文档](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/TextCore.Text.FontAsset.html)
- [TextMeshPro Font Asset Creator 文档](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/manual/FontAssetsCreator.html)
- [思源黑体（Source Han Sans）- GitHub](https://github.com/adobe-fonts/source-han-sans)
- [Noto Fonts - Google](https://fonts.google.com/noto)
- [Unicode CJK 统一汉字区段说明](https://www.unicode.org/faq/han_unification.html)
- [Unity UI Toolkit USS 属性参考](https://docs.unity3d.com/2022.3/Documentation/Manual/UIE-USS-Properties-Reference.html)
- [RTLTMPro 开源库](https://github.com/pnarimani/RTLTMPro) 