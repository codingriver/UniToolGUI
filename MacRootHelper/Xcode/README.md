# UniToolMacRootHelper

原生 helper 与 bridge 的源码位于 `MacRootHelper/Sources/`。

当前仓库使用 `MacRootHelper/Scripts/build.sh` 作为统一构建入口，产物直接输出到 Unity 插件目录。
如需后续补齐完整 `.xcodeproj` 调试工程，可在此目录基础上继续扩展，不影响现有脚本化构建链路。
