/// <summary>
/// 系统主题与 DPI 信息接口。
/// </summary>
public interface IThemeService
{
    /// <summary>是否处于深色模式</summary>
    bool IsDarkMode();
    /// <summary>当前 DPI 缩放（1.0 = 100%，1.5 = 150%）</summary>
    float GetDpiScale();
    /// <summary>系统强调色（ARGB）</summary>
    UnityEngine.Color32 GetAccentColor();
}
