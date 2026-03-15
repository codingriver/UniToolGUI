# Mac 系统托盘原生插件

## 编译

在 **Mac** 上执行：

```bash
cd Assets/Plugins/MacOS/MacTray
chmod +x build.sh
./build.sh
```

需要安装 Xcode Command Line Tools：`xcode-select --install`

编译成功后会在当前目录生成 `MacTray.bundle`。

## 部署

将 `MacTray.bundle` 复制到 `Assets/Plugins/` 目录（与 MacTrayPlugin.cs 同级），或在 Unity 中为该 bundle 的 .meta 设置 **Select platforms for plugin** 仅勾选 **Mac**。

## 依赖

- macOS 10.9+
- AppKit、Foundation 框架
