using System.Collections.Generic;

/// <summary>空实现（不支持的平台），所有方法安全无操作。</summary>
public class NullFileDialog : IFileDialog
{
    public static readonly NullFileDialog Instance = new NullFileDialog();
    public string   OpenFilePanel(string t, string f, string e=null, string d=null) => null;
    public string[] OpenFilePanelMulti(string t, string f, string e=null, string d=null) => null;
    public string   SaveFilePanel(string t, string f, string e=null, string d=null, string n=null) => null;
    public string   OpenFolderPanel(string t, string d=null) => null;
    public string   CreateFilter(params string[] pairs)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < pairs.Length - 1; i += 2) { sb.Append(pairs[i]); sb.Append('\0'); sb.Append(pairs[i+1]); sb.Append('\0'); }
        sb.Append('\0'); return sb.ToString();
    }
}

/// <summary>文件对话框平台实现（委托给 WindowsFileDialog）。</summary>
public class NativeFileDialog : IFileDialog
{
    public static readonly NativeFileDialog Instance = new NativeFileDialog();
    public string   OpenFilePanel(string t, string f, string e=null, string d=null) => WindowsFileDialog.OpenFilePanel(t, f, e, d);
    public string[] OpenFilePanelMulti(string t, string f, string e=null, string d=null) => WindowsFileDialog.OpenFilePanelMulti(t, f, e, d);
    public string   SaveFilePanel(string t, string f, string e=null, string d=null, string n=null) => WindowsFileDialog.SaveFilePanel(t, f, e, d, n);
    public string   OpenFolderPanel(string t, string d=null) => WindowsFileDialog.OpenFolderPanel(t, d);
    public string   CreateFilter(params string[] pairs) => WindowsFileDialog.CreateFilter(pairs);
}
