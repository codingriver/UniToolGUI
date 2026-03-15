/// <summary>
/// 系统信息查询接口。
/// </summary>
public interface ISystemInfo
{
    (int width, int height) GetPrimaryScreenSize();
    (int width, int height) GetVirtualScreenSize();
    (bool onAC, int percent, int remainingSeconds) GetBatteryInfo();
    uint GetIdleTimeSeconds();
    string GetComputerName();
    string GetUserName();
}
