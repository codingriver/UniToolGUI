# MacRootHelper

`MacRootHelper` 提供 macOS 下的 Root Helper（launchd + XPC），以及 Unity 侧的 XPC Bridge，配合 Unity 插件实现受信任调用方的提权操作。

---

## 目录结构

```
MacRootHelper/
├── Sources/
│   ├── RootHelper/        # Root Helper 主进程源码（Objective-C）
│   └── Bridge/            # XPC Bridge bundle 源码（供 Unity C# 调用）
├── Resources/
│   └── plists/            # launchd plist 模板
└── Scripts/               # 构建、安装、卸载、信任刷新、验收脚本
```

Unity 插件侧核心文件：

```
Assets/Plugins/NativeKit/
├── MacHelperBridge.cs         # P/Invoke 层（调用 UniToolXpcBridge.bundle）
├── MacHelperService.cs        # XPC 请求/事件管理（静态服务层）
├── MacHelperInstallService.cs # 安装/卸载/刷新信任/路径管理
├── MacHelperModels.cs         # 数据模型（Request/Event/Status/Payload）
└── MacOS/
    ├── UniToolXpcBridge.bundle              # XPC Bridge 产物（bundle）
    └── HelperArtifacts/
        ├── roothelper/com.unitool.roothelper  # helper 编译产物
        └── package/                           # 打包目录（含安装脚本）
Assets/Editor/
└── MacHelperBuildHook.cs      # Unity 构建钩子（pre/post build）
```

---

## 已实现 Action

Root Helper 支持以下 action（详见 `Sources/RootHelper/root_helper_main.m`）：

| action | 需信任 | 描述 |
|--------|--------|------|
| `helper.ping` | ✅ | 健康检查，返回 label 与 pid |
| `helper.status` | ✅ | 返回运行信息（pid、日志路径、备份目录、信任文件路径） |
| `trust.refresh` | ❌ | 写入新 token 到 trust.json（初次建立信任用） |
| `hosts.update` | ✅ | 以 root 权限更新 hosts，自动备份原文件 |
| `hosts.restore` | ✅ | 从最新备份恢复 hosts |
| `shell.exec` | ✅ | 以 root 权限执行任意 shell 命令，支持超时与流式 stdout/stderr |

---

## 安全策略

- 信任校验采用 **token-only** 模式：helper 校验请求中的 `token` 字段与 `/Users/Shared/UniTool/helper/trust.json` 中记录的 token 一致后放行。
- 调用方路径（`callerPath`）与 SHA256（`callerHash`）**仅记录在 helper 日志中**，不参与校验。
- `trust.refresh` 无需 token 即可执行（用于初次写入 trust.json），其余所有 action 均需通过 token 校验；拒绝时返回 `eventType=failed exitCode=403`。
- 默认 token 为 `codingriver_unitool_token`，**发布前请通过 `MacHelperInstallService.TrustToken` 替换为项目专属 token**，使用默认 token 时 Unity 侧会打印 `LogWarning`。
- XPC 服务仅通过 MachService 暴露，helper 以 root 运行，整个 app 进程本身不需要 root 权限。

---

## 构建

统一构建入口：

```zsh
MacRootHelper/build.sh --all
```

参数说明：

| 参数 | 效果 |
|------|------|
| `--all`（默认）| 同时构建 helper + bridge + 同步 package |
| `--helper-only` | 仅编译 helper 二进制 |
| `--bridge-only` | 仅编译 XPC Bridge bundle |

构建产物：

| 产物 | 路径 |
|------|------|
| Helper 二进制 | `Assets/Plugins/NativeKit/MacOS/HelperArtifacts/roothelper/com.unitool.roothelper` |
| XPC Bridge bundle | `Assets/Plugins/NativeKit/MacOS/UniToolXpcBridge.bundle` |
| Package 目录 | `Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package/` |

Package 目录内容（同步到 `.app` 的 `Resources/PrivilegedHelper`）：

| 文件 | 用途 |
|------|------|
| `com.unitool.roothelper` | Helper 主进程二进制 |
| `com.unitool.roothelper.plist` | LaunchDaemon 配置 |
| `install_helper.sh` | 安装脚本（需 sudo） |
| `uninstall_helper.sh` | 卸载脚本（需 sudo） |
| `refresh_trust.sh` | 刷新 token 脚本（需 sudo） |

说明：这些脚本的**源文件**位于 `MacRootHelper/scripts/`，构建时由 `build.sh` 复制到 package 目录。

也可直接用 clang 单独编译（不依赖 build.sh）：

```zsh
clang -arch arm64 -arch x86_64 -fobjc-arc -framework Foundation -lproc \
  -o Assets/Plugins/NativeKit/MacOS/HelperArtifacts/roothelper/com.unitool.roothelper \
  MacRootHelper/Sources/RootHelper/root_helper_main.m
```

---

## 安装与卸载

### 安装（需 sudo）

脚本参数为 `--token <token>`（与 `MacHelperInstallService.TrustToken` 保持一致）：

```zsh
sudo "<app>/Contents/Resources/PrivilegedHelper/install_helper.sh" \
  --token "codingriver_unitool_token"
```

Unity 内通过「安装 Helper」按钮触发，走 `osascript with administrator privileges`，自动弹出系统权限弹窗。

### 卸载（需 sudo）

```zsh
sudo "<app>/Contents/Resources/PrivilegedHelper/uninstall_helper.sh"
```

### 刷新信任（需 sudo）

```zsh
sudo "<app>/Contents/Resources/PrivilegedHelper/refresh_trust.sh" \
  --token "codingriver_unitool_token"
```

---

## 运行时路径

| 类型 | 路径 |
|------|------|
| Helper 安装路径 | `/Library/PrivilegedHelperTools/com.unitool.roothelper` |
| LaunchDaemon plist | `/Library/LaunchDaemons/com.unitool.roothelper.plist` |
| 信任文件 | `/Users/Shared/UniTool/helper/trust.json` |
| 日志目录 | `/Users/Shared/UniTool/logs/` |
| helper.log | `/Users/Shared/UniTool/logs/helper.log` |
| helper stdout | `/Users/Shared/UniTool/logs/helper.stdout.log` |
| helper stderr | `/Users/Shared/UniTool/logs/helper.stderr.log` |
| 备份目录 | `/Users/Shared/UniTool/helper/backups/` |

Package 运行时路径由 `MacHelperInstallService.GetRuntimePackageDirectory()` 多候选探测：

1. `Application.dataPath/Resources/PrivilegedHelper`（`.app` 标准路径）
2. `parent(dataPath)/Resources/PrivilegedHelper`
3. `parent(parent(dataPath))/Resources/PrivilegedHelper`
4. `Application.dataPath/Plugins/NativeKit/MacOS/HelperArtifacts/package`（兜底）

---

## Unity 侧调用接口

### 编译宏

所有 macOS 提权功能均受以下宏保护：

```
(UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
```

在 Editor 中调试时，定义 `MAC_HELPER_IN_EDITOR` 即可启用完整功能。

### 初始化与连接

```csharp
NativeKit.MacHelperService.Initialize();
bool ok = NativeKit.MacHelperService.Connect();
```

`Initialize()` 幂等，可重复调用。`Connect()` 内部会自动调用 `Initialize()`。

### 诊断接口

```csharp
// ping（只读，需 helper 运行）
if (NativeKit.MacHelperService.Ping(out var pingEvt, out var pingErr))
    Debug.Log(pingEvt.Message);

// status（只读，返回 pid/路径等）
if (NativeKit.MacHelperService.QueryStatus(out var statusEvt, out var statusErr))
    Debug.Log(statusEvt.PayloadJson);
```

### hosts.update

```csharp
bool ok = NativeKit.MacHelperService.SubmitHostsUpdate(
    "/etc/hosts", newContent, Debug.Log, out var err);
```

### shell.exec（流式）

```csharp
string requestId = NativeKit.MacHelperService.SubmitShellCommand(
    "ls -la /etc",
    evt => Debug.Log($"[{evt.EventType}] {evt.Message}")
);
```

事件流：`accepted` → `progress` → `stdout/stderr`（0-N 次）→ `completed/failed`（仅一次，已修复重复事件 bug）

### 通用 Submit

```csharp
string requestId = NativeKit.MacHelperService.Submit(
    "shell.exec",
    JsonUtility.ToJson(new NativeKit.MacShellExecPayload { command = "whoami" }),
    60,
    Application.productName,
    evt => Debug.Log(evt.Message)
);
```

### 全局事件监听

```csharp
NativeKit.MacHelperService.OnEvent += evt =>
{
    Debug.Log($"[{evt.Action}][{evt.EventType}] {evt.Message}");
};
```

### 安装管理（MacHelperInstallService）

```csharp
// 设置 token（发布前必改）
NativeKit.MacHelperInstallService.TrustToken = "your-project-token";

// 安装（弹出系统授权窗口）
bool ok = NativeKit.MacHelperInstallService.Install(out var msg);

// 卸载
bool ok = NativeKit.MacHelperInstallService.Uninstall(out var msg);

// 刷新信任
bool ok = NativeKit.MacHelperInstallService.RefreshTrust(out var msg);

// 查询当前状态
var status = NativeKit.MacHelperInstallService.QueryStatus();
Debug.Log(status.isInstalled + " / " + status.isConnected + " / " + status.message);
```

---

## XPC JSON 协议

### 请求字段（MacHelperRequest）

| 字段 | 类型 | 说明 |
|------|------|------|
| `requestId` | string | 请求唯一 ID（自动生成） |
| `action` | string | 动作名 |
| `payload` | string | JSON 字符串 |
| `token` | string | 信任 token |
| `timeoutSec` | int | 超时秒数（1~600，默认 60） |
| `source` | string | 来源标识（默认 `Application.productName`） |

### 事件字段（MacHelperEvent）

| 字段 | 类型 | 说明 |
|------|------|------|
| `requestId` | string | 对应请求 ID |
| `action` | string | 动作名 |
| `eventType` | string | `accepted`/`progress`/`stdout`/`stderr`/`completed`/`failed` |
| `ok` | bool | 是否成功 |
| `exitCode` | int | 退出码（403=信任失败，124=超时） |
| `message` | string | 消息文本 |
| `payloadJson` | string | 附加 JSON 数据 |

### Payload 约定

| action | payload |
|--------|---------|
| `helper.ping` | `{}` |
| `helper.status` | `{}` |
| `trust.refresh` | `{"token":"..."}` |
| `hosts.update` | `{"targetPath":"/etc/hosts","content":"..."}` |
| `hosts.restore` | `{"targetPath":"/etc/hosts"}` |
| `shell.exec` | `{"command":"..."}` |

---

## Unity 构建钩子（MacHelperBuildHook）

文件：`Assets/Editor/MacHelperBuildHook.cs`

### Pre-build 检查（`IPreprocessBuildWithReport`）

仅在 `BuildTarget.StandaloneOSX` 时执行，缺少以下任一文件将抛出 `BuildFailedException`：

- `Assets/Plugins/NativeKit/MacOS/UniToolXpcBridge.bundle`
- `Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package/com.unitool.roothelper`
- `Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package/com.unitool.roothelper.plist`
- `Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package/install_helper.sh`
- `Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package/uninstall_helper.sh`
- `Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package/refresh_trust.sh`

### Post-build 拷贝（`[PostProcessBuild(200)]`）

构建完成后自动将 `HelperArtifacts/package/` 内容复制到：

```
YourApp.app/Contents/Resources/PrivilegedHelper/
```

并通过 `/bin/chmod 755` 恢复以下文件的可执行权限：

- `com.unitool.roothelper`
- `install_helper.sh`
- `uninstall_helper.sh`
- `refresh_trust.sh`

---

## 验收脚本

脚本路径：`MacRootHelper/test/acceptance_check.sh`

```zsh
# 基础验收（不含重启测试）
MacRootHelper/test/acceptance_check.sh

# 含 helper 自动重启验证（需要 sudo）
MacRootHelper/test/acceptance_check.sh --test-restart
```

检查项：

| 检查项 | 说明 |
|--------|------|
| package 产物完整性 | 5 个文件全部存在 |
| 系统安装文件 | helper 二进制、plist、trust.json 存在 |
| helper.log 存在 | 信息项（不存在不计失败） |
| launchctl 服务注册 | `launchctl print system/com.unitool.roothelper` 成功 |
| 自动重启（--test-restart） | kill helper 后 2s 内自动拉起新 pid |

> 说明：launchd 注册成功但 `state != running` 属于 on-demand idle 状态，验收脚本不判为失败。

---

## 常见问题与排查

### 1) 安装失败：缺少 helper 脚本

现象：
```
Root Helper 安装失败: 缺少 helper 脚本: .../PrivilegedHelper/install_helper.sh
```

原因：`GetRuntimePackageDirectory()` 未命中有效路径。

排查：

```csharp
Debug.Log(NativeKit.MacHelperInstallService.GetRuntimePackageDirectory());
```

确认路径下文件存在。若是 Unity 打包，确认 `PostProcessBuild` 已执行（日志中有 `[MacHelperBuild] 已复制 Root Helper 资源到: ...`）。

### 2) 诊断 ping/status 超时或 403

排查步骤：

```zsh
# 查看服务状态
launchctl print system/com.unitool.roothelper

# 查看 helper 日志
tail -n 100 /Users/Shared/UniTool/logs/helper.log

# 查看 trust.json
cat /Users/Shared/UniTool/helper/trust.json
```

若 403：token 不一致，刷新信任：

```zsh
sudo "<app>/Contents/Resources/PrivilegedHelper/refresh_trust.sh" --token "your-token"
```

若服务未运行，手动拉起：

```zsh
sudo launchctl kickstart -k system/com.unitool.roothelper
```

### 3) shell.exec 重复终态事件

已在 `root_helper_main.m` 中修复（引入 `finalEventSent` + `guardQueue`）。
升级后需重新编译 helper：

```zsh
MacRootHelper/build.sh --helper-only
# 或直接调用 clang
clang -arch arm64 -arch x86_64 -fobjc-arc -framework Foundation -lproc \
  -o Assets/Plugins/NativeKit/MacOS/HelperArtifacts/roothelper/com.unitool.roothelper \
  MacRootHelper/Sources/RootHelper/root_helper_main.m
```

### 4) Editor 下功能不可用

在 Unity Player Settings > Scripting Define Symbols 添加：

```
MAC_HELPER_IN_EDITOR
```

同时确保 bridge bundle 已构建，并且当前机器上 helper 已安装。

### 5) 授权弹窗文案

通过 `MacHelperInstallService.AuthorizationPrompt` 设置：

```csharp
NativeKit.MacHelperInstallService.AuthorizationPrompt = "更新Hosts权限申请";
```

系统弹窗的应用名/图标由 `.app` 签名决定，不可通过代码覆盖。

### 6) launchctl 显示 not running（on-demand idle）

这是正常状态。MachService 模式下 helper 在有连接时才运行，断开连接后会进入 idle。
验收脚本不将此视为失败。需要激活时 XPC connect 即可自动唤醒。

---

## Unity 接入流程（SDK 集成）

### 1) 构建 Root Helper 产物

```zsh
MacRootHelper/build.sh --all
```

确保以下文件已生成：

- `Assets/Plugins/NativeKit/MacOS/UniToolXpcBridge.bundle`
- `Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package/`（包含 helper + 脚本）

### 2) Unity 侧引用文件

以下代码文件必须保留：

- `Assets/Plugins/NativeKit/MacHelperBridge.cs`
- `Assets/Plugins/NativeKit/MacHelperService.cs`
- `Assets/Plugins/NativeKit/MacHelperInstallService.cs`

以下资源需要随 App 打包：

- `Assets/Plugins/NativeKit/MacOS/UniToolXpcBridge.bundle`
- `Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package/`

### 3) Unity 构建钩子

`Assets/Editor/MacHelperBuildHook.cs` 会在打包时检查上述产物并复制到：

```
YourApp.app/Contents/Resources/PrivilegedHelper/
```

### 4) 启动时初始化

```csharp
NativeKit.MacHelperService.Initialize();
NativeKit.MacHelperService.Connect();
```

### 5) 设置信任 Token

```csharp
NativeKit.MacHelperInstallService.TrustToken = "your-token";
```

然后执行一次安装或刷新：

```csharp
NativeKit.MacHelperInstallService.RefreshTrust(out _);
```

### 6) 安装 / 卸载 / 刷新信任

```csharp
NativeKit.MacHelperInstallService.Install(out var installMsg);
NativeKit.MacHelperInstallService.Uninstall(out var uninstallMsg);
NativeKit.MacHelperInstallService.RefreshTrust(out var trustMsg);
```

### 7) Editor 下测试（可选）

在 Unity `Scripting Define Symbols` 加入：

```
MAC_HELPER_IN_EDITOR
```

然后即可在 macOS Editor 中走同样的连接与安装逻辑。
