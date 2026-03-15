/// <summary>
/// 文件/文件夹选择对话框接口。
/// </summary>
public interface IFileDialog
{
    /// <summary>打开单个文件选择对话框。返回路径，取消返回 null。</summary>
    string OpenFilePanel(string title, string filter, string defaultExt = null, string initialDir = null);

    /// <summary>打开多文件选择对话框。返回路径数组，取消返回 null。</summary>
    string[] OpenFilePanelMulti(string title, string filter, string defaultExt = null, string initialDir = null);

    /// <summary>打开保存文件对话框。返回路径，取消返回 null。</summary>
    string SaveFilePanel(string title, string filter, string defaultExt = null, string initialDir = null, string defaultFileName = null);

    /// <summary>打开文件夹选择对话框。返回路径，取消返回 null。</summary>
    string OpenFolderPanel(string title, string initialDir = null);

    /// <summary>构建过滤器字符串（成对传入：描述, 模式）</summary>
    string CreateFilter(params string[] filterPairs);
}
