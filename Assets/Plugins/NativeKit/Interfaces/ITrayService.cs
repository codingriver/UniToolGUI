using System;
using System.Collections.Generic;

/// <summary>
/// 系统托盘服务接口。
/// </summary>
public interface ITrayService
{
    /// <summary>托盘图标创建后触发</summary>
    event Action OnTrayIconCreated;
    /// <summary>托盘图标销毁后触发</summary>
    event Action OnTrayIconDestroyed;
    /// <summary>全局热键触发（id 为注册时的 ID，仅 Windows）</summary>
    event Action<int> OnHotkeyPressed;

    void Initialize();
    void Shutdown();
    void SetTooltip(string tooltip);
    void RegisterMenuItems(IEnumerable<TrayMenuItem> items);
    void UnregisterMenuItems(IEnumerable<TrayMenuItem> items);
    void ClearMenuItems();
    void RefreshMenu();
    void ShowMainWindow();
    /// <param name="iconType">0=无 1=信息 2=警告 3=错误</param>
    void ShowBalloonTip(string title, string message, uint iconType = 1, uint timeoutMs = 5000);
}
