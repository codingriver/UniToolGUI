/// <summary>空实现 Toast（不支持的平台）。</summary>
public class NullToastService : IToastService
{
    public static readonly NullToastService Instance = new NullToastService();
    public bool Show(string title, string message, string imagePath = null) => false;
}

/// <summary>系统通知实现（委托给 WindowsToast）。</summary>
public class NativeToastService : IToastService
{
    public static readonly NativeToastService Instance = new NativeToastService();
    public bool Show(string title, string message, string imagePath = null)
        => WindowsToast.Show(title, message, imagePath);
}
