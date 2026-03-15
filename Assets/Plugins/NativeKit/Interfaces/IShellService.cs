/// <summary>
/// Shell 操作接口（打开 URL、文件、文件夹等）。
/// </summary>
public interface IShellService
{
    bool OpenUrl(string url);
    bool OpenFile(string filePath);
    /// <param name="filePath">若提供，在文件管理器中选中该文件</param>
    bool OpenFolder(string folderPath, string filePath = null);
}
