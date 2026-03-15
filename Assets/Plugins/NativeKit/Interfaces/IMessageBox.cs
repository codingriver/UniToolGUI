/// <summary>
/// 原生消息框接口。
/// </summary>
public interface IMessageBox
{
    void Info(string text, string caption = "信息");
    void Warning(string text, string caption = "警告");
    void Error(string text, string caption = "错误");
    /// <summary>确认对话框，返回 true 表示用户点击确定/OK</summary>
    bool Confirm(string text, string caption = "确认");
    /// <summary>是/否对话框，返回 true 表示用户点击是/Yes</summary>
    bool YesNo(string text, string caption = "请选择");
}
