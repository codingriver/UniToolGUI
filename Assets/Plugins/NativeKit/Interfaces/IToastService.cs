/// <summary>
/// 系统 Toast / 桌面通知接口。
/// </summary>
public interface IToastService
{
    /// <summary>显示桌面通知</summary>
    /// <param name="imagePath">可选图片路径（仅部分平台支持）</param>
    bool Show(string title, string message, string imagePath = null);
}
