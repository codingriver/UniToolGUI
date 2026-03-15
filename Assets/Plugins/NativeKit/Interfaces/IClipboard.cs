/// <summary>
/// 剪贴板操作接口。
/// </summary>
public interface IClipboard
{
    /// <summary>获取剪贴板文本，失败返回 null</summary>
    string GetText();
    /// <summary>设置剪贴板文本</summary>
    bool SetText(string text);
    /// <summary>检查剪贴板是否包含文本</summary>
    bool HasText();
}
