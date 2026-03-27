# MacRootHelper

`MacRootHelper` 提供 macOS 下的 Root Helper（launchd + XPC），以及 Unity 侧的 XPC Bridge，配合 Unity 插件实现受信任调用方的提权操作。

## 目录结构

- `Sources/RootHelper`：Root Helper 源码（launchd + XPC）
- `Sources/Bridge`：XPC Bridge 源码（供 Unity 调用）
- `Resources/plists`：launchd plist 模板
- `Scripts`：构建、安装、卸载、信任刷新、验收检查脚本
- `Xcode`：说明文档

## 已实现功能

Root Helper 支持的 action 列表（详见 `Sources/RootHelper/root_helper_main.m`）：

- `helper.ping`：健康检查，返回 helper 的 label 与 pid
- `helper.status`：返回 helper 运行信息（label、pid、日志路径、备份目录、信任文件路径）
- `trust.refresh`：刷新信任（写入调用方路径与 SHA256）
- `hosts.update`：更新 hosts，自动备份
- `hosts.restore`：从最新备份恢复 hosts
- `shell.exec`：以 root 权限执行 shell 命令，支持超时与流式 stdout/stderr

安全策略：

- 仅允许通过信任校验的调用方（`allowedPath` + `sha256`）访问 helper
- 信任文件路径：`/Users/Shared/UniTool/helper/trust.json`

## 构建与产物

统一构建入口：`MacRootHelper/Scripts/build.sh`

```zsh
MacRootHelper/Scripts/build.sh --all
```

构建产物：

- Helper 二进制：`Assets/Plugins/NativeKit/MacOS/HelperArtifacts/roothelper/com.unitool.roothelper`
- XPC Bridge bundle：`Assets/Plugins/NativeKit/MacOS/UniToolXpcBridge.bundle`
- 打包目录：`Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package`

打包目录内容：

- `com.unitool.roothelper`
- `com.unitool.roothelper.plist`
- `install_helper.sh`
- `uninstall_helper.sh`
- `refresh_trust.sh`

## 一键打包脚本

脚本位置：`MacRootHelper/Scripts/package_oneclick.sh`

作用：

- 调用现有 `build.sh` 完成构建
- 将 `Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package` 打包为 zip
- 不改变现有打包逻辑，仅新增“一键执行”入口

用法：

```zsh
MacRootHelper/Scripts/package_oneclick.sh
```

产物输出：

- `MacRootHelper/Build/UniToolRootHelperPackage-YYYYMMDD-HHMMSS.zip`

## 安装与卸载

安装（需要管理员权限）：

```zsh
APP_EXE="/Applications/UniTool.app/Contents/MacOS/UniTool"
APP_SHA256="$(shasum -a 256 "${APP_EXE}" | awk '{print $1}')"
sudo Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package/install_helper.sh \
  --app-exe "${APP_EXE}" \
  --app-sha256 "${APP_SHA256}"
```

卸载：

```zsh
sudo Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package/uninstall_helper.sh
```

刷新信任：

```zsh
sudo Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package/refresh_trust.sh \
  --app-exe "${APP_EXE}" \
  --app-sha256 "${APP_SHA256}"
```

## 测试与验收

验收脚本：

```zsh
MacRootHelper/Scripts/acceptance_check.sh
```

测试 helper 自动重启：

```zsh
MacRootHelper/Scripts/acceptance_check.sh --test-restart
```

常用运行态路径：

- Helper 安装路径：`/Library/PrivilegedHelperTools/com.unitool.roothelper`
- LaunchDaemon plist：`/Library/LaunchDaemons/com.unitool.roothelper.plist`
- 日志目录：`/Users/Shared/UniTool/logs`
- 信任文件：`/Users/Shared/UniTool/helper/trust.json`
- 备份目录：`/Users/Shared/UniTool/helper/backups`

## Unity 侧调用接口

核心入口类：

- `Assets/Plugins/NativeKit/MacHelperBridge.cs`
- `Assets/Plugins/NativeKit/MacHelperService.cs`
- `Assets/Plugins/NativeKit/MacHelperInstallService.cs`

示例：连接 helper

```csharp
NativeKit.MacHelperService.Initialize();
bool ok = NativeKit.MacHelperService.Connect();
```

示例：ping

```csharp
if (NativeKit.MacHelperService.Ping(out var evt, out var err))
{
    Debug.Log(evt.PayloadJson);
}
```

示例：hosts.update

```csharp
NativeKit.MacHelperService.SubmitHostsUpdate("/etc/hosts", content, Debug.Log, out var err);
```

示例：shell.exec（带流式回调）

```csharp
NativeKit.MacHelperService.Submit(
    "shell.exec",
    JsonUtility.ToJson(new NativeKit.MacShellExecPayload { command = "ls -la" }),
    60,
    Application.productName,
    evt => Debug.Log($"[{evt.EventType}] {evt.Message}")
);
```

## XPC JSON 协议

请求字段（`MacHelperRequest`）：

- `requestId`：请求 ID
- `action`：动作名
- `payload`：JSON 字符串
- `timeoutSec`：超时秒数
- `source`：来源标识

事件字段（`MacHelperEvent`）：

- `requestId`
- `action`
- `eventType`：`accepted` | `progress` | `stdout` | `stderr` | `completed` | `failed`
- `ok`：是否成功
- `exitCode`：退出码
- `message`：消息文本
- `payloadJson`：JSON 字符串

动作与 payload 约定：

- `helper.ping`：`{}`
- `helper.status`：`{}`
- `trust.refresh`：`{"appExe":"...","appSha256":"..."}`
- `hosts.update`：`{"targetPath":"/etc/hosts","content":"..."}`
- `hosts.restore`：`{"targetPath":"/etc/hosts"}`
- `shell.exec`：`{"command":"..."}`

## 构建与打包在 Unity 构建流程中的位置

Unity 构建时会通过 `Assets/Editor/MacHelperBuildHook.cs` 检查：

- `Assets/Plugins/NativeKit/MacOS/UniToolXpcBridge.bundle` 是否存在
- `Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package` 是否存在且包含完整文件

构建完成后，Unity 会把 `HelperArtifacts/package` 复制到：

- `YourApp.app/Contents/Resources/PrivilegedHelper`
