# Unity Personal 许可证与 GitHub Secret 配置

本仓库的 CI 使用 [GameCI unity-builder](https://game.ci/docs/github/getting-started/)，需要在仓库 Secret 中提供 **Unity 许可证文件（`.ulf`）的完整文本**，变量名为 `**UNITY_LICENSE`**。

## 适用情况

- 使用 **Unity Personal（个人免费版）**，且你符合 Unity 对 Personal 的资格要求（见 [Unity 条款](https://unity.com/legal)）。
- 在 **已安装与你项目一致的国际版 Editor**（例如 `2022.3.62f3`）的机器上操作。

## 如何获取 `.ulf` 内容

### 方法一：在本地 Unity Editor 中（常用）

1. 打开 Unity Hub，用 **同一 Unity 账号** 登录。
2. 打开本项目（Editor 版本与 `ProjectSettings/ProjectVersion.txt` 一致）。
3. 菜单 **Help → Manage License**（或 **Manage your license**）。
4. 选择 **Manual activation**（手动激活）或按界面指引 **导出/保存许可证文件**（不同 Unity 版本文案可能略有差异）。
5. 若得到的是 **请求文件（`.alf`）**：到 Unity 官网的许可证/激活页面，上传 `.alf`，下载返回的 `**.ulf`**。
6. 用文本编辑器打开 `**.ulf**`，**全选复制**其中全部内容（XML 文本）。

### 方法二：参考 GameCI 官方「激活」文档

以官网最新步骤为准：  
[https://game.ci/docs/github/activation/](https://game.ci/docs/github/activation/)

其中会说明 Personal / Pro 在 CI 中的差异与注意事项。

## 在 GitHub 仓库里怎么填

1. 打开 GitHub：**Settings → Secrets and variables → Actions**。
2. **New repository secret**。
3. **Name**：`UNITY_LICENSE`（必须完全一致）。
4. **Secret**：粘贴上一步 `**.ulf` 文件的完整文本**（不要只粘贴路径；不要加引号）。
5. 保存。

之后运行 **Unity Build & Release** 工作流时，`unity-builder` 会读取该 Secret 并在构建机里完成激活与归还许可证（按 GameCI 行为）。

## 注意

- **不要把 `.ulf` 或 Secret 内容提交到仓库或发给不可信渠道。**
- 许可证可能 **过期或失效**（例如重装系统、换机、Unity 策略变更）。若 CI 报激活失败，按同样流程重新生成 `.ulf` 并 **更新** `UNITY_LICENSE`。
- 若将来改用 Unity Pro/Plus 等，仍通过 GameCI 文档中的对应方式配置（可能除 `UNITY_LICENSE` 外还有其它变量）。

