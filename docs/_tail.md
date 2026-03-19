
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
