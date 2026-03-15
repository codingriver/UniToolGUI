/// <summary>空实现消息框（不支持的平台）。</summary>
public class NullMessageBox : IMessageBox
{
    public static readonly NullMessageBox Instance = new NullMessageBox();
    public void Info(string t, string c="信息")    { }
    public void Warning(string t, string c="警告") { }
    public void Error(string t, string c="错误")   { }
    public bool Confirm(string t, string c="确认") => false;
    public bool YesNo(string t, string c="请选择") => false;
}

/// <summary>原生消息框实现（委托给 WindowsMessageBox）。</summary>
public class NativeMessageBox : IMessageBox
{
    public static readonly NativeMessageBox Instance = new NativeMessageBox();
    public void Info(string t, string c="信息")    => WindowsMessageBox.Info(t, c);
    public void Warning(string t, string c="警告") => WindowsMessageBox.Warning(t, c);
    public void Error(string t, string c="错误")   => WindowsMessageBox.Error(t, c);
    public bool Confirm(string t, string c="确认") => WindowsMessageBox.Confirm(t, c);
    public bool YesNo(string t, string c="请选择") => WindowsMessageBox.YesNo(t, c);
}
