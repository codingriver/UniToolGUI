/// <summary>
/// 开机自启接口。
/// </summary>
public interface IStartupService
{
    bool IsStartupEnabled();
    bool EnableStartup(string exePath = null, string keyName = null);
    bool DisableStartup(string keyName = null);
    bool ToggleStartup(string exePath = null);
}
