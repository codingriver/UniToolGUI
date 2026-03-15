lines = open(r'd:\UniToolGUI\Assets\Plugins\MacTrayPlugin.cs', encoding='utf-8').readlines()
# Find '#endif' near end and insert before it
for i in range(len(lines)-1, -1, -1):
    if lines[i].strip() == '#endif':
        insert_idx = i
        break

new_methods = '''    /// <summary>从文件路径设置托盘图标（PNG/ICNS，建议 18x18 px）。</summary>
    public static bool SetIcon(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return false;
        return MacTray_SetIcon(imagePath) != 0;
    }

    /// <summary>从 PNG 字节数组设置托盘图标（可由 Texture2D.EncodeToPNG() 提供）。</summary>
    public static bool SetIconFromData(byte[] pngData)
    {
        if (pngData == null || pngData.Length == 0) return false;
        return MacTray_SetIconFromData(pngData, pngData.Length) != 0;
    }
'''

lines.insert(insert_idx, new_methods)
open(r'd:\UniToolGUI\Assets\Plugins\MacTrayPlugin.cs', 'w', encoding='utf-8').writelines(lines)
print('done, lines:', len(lines))
