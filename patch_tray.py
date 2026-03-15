# -*- coding: utf-8 -*-
# Patch TrayIconService.cs:
# 1. Add LoadImage DllImport + constants (Windows section)
# 2. Add _hCustomIcon field
# 3. Add public SetIcon(string) and SetIcon(Texture2D) methods
# 4. Wire in Mac platform branch

path = r'd:\UniToolGUI\Assets\Plugins\TrayIconService.cs'
lines = open(path, encoding='utf-8').readlines()
content = ''.join(lines)

# ── 1. Add LoadImage import after LoadIcon import (Windows section) ──────
old_load_icon = '    [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern IntPtr LoadIcon(IntPtr h,IntPtr n);'
new_load_icon = '''    [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern IntPtr LoadIcon(IntPtr h,IntPtr n);
    [DllImport("user32.dll",CharSet=CharSet.Auto,SetLastError=true)] static extern IntPtr LoadImage(IntPtr hInst,string name,uint type,int cx,int cy,uint fuLoad);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);'''
content = content.replace(old_load_icon, new_load_icon, 1)

# ── 2. Add constants after TPM_RB=2 ──────────────────────────────────────
old_const = '    const uint MF_STR=0,MF_SEP=0x800,MF_CHK=8,TPM_RB=2;'
new_const = '''    const uint MF_STR=0,MF_SEP=0x800,MF_CHK=8,TPM_RB=2;
    const uint IMAGE_ICON=1,LR_LOADFROMFILE=0x10,LR_DEFAULTSIZE=0x40;'''
content = content.replace(old_const, new_const, 1)

# ── 3. Add _hCustomIcon field after _trayAdded field ─────────────────────
old_field = '    bool _trayAdded;'
new_field = '    bool _trayAdded;\n    IntPtr _hCustomIcon;'
content = content.replace(old_field, new_field, 1)

# ── 4. Add Windows SetIcon platform method after PlatformShowBalloon ──────
old_balloon_win = '''    void PlatformShowBalloon(string title,string msg,uint iconType,uint ms) {
        var n=MakeNid(); n.uFlags=NIF_INFO; n.szInfoTitle=title??""; n.szInfo=msg??""; n.dwInfoFlags=iconType; n.uTimeout=ms;
        Shell_NotifyIcon(NIM_MOD,ref n);
    }'''
new_balloon_win = old_balloon_win + '''
    void PlatformSetIcon(string iconPath) {
        if (string.IsNullOrEmpty(iconPath)) return;
        var hNew = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE|LR_DEFAULTSIZE);
        if (hNew == IntPtr.Zero) { Debug.LogWarning("[Tray] LoadImage failed: " + iconPath); return; }
        if (_hCustomIcon != IntPtr.Zero) DestroyIcon(_hCustomIcon);
        _hCustomIcon = hNew;
        var n = MakeNid(); n.uFlags = NIF_ICO; n.hIcon = _hCustomIcon;
        Shell_NotifyIcon(NIM_MOD, ref n);
    }
    void PlatformSetIconFromData(byte[] pngData) {
        // Windows 不支持直接从内存 PNG 加载，降级为忽略（需传 .ico 文件路径）
        Debug.LogWarning("[Tray] Windows 不支持从内存数据设置托盘图标，请使用 SetIcon(string .ico 路径)");
    }'''
content = content.replace(old_balloon_win, new_balloon_win, 1)

# ── 5. Add Mac platform methods ───────────────────────────────────────────
old_mac_balloon = '    void PlatformShowBalloon(string title,string msg,uint _1,uint _2) => MacTrayPlugin.ShowBalloon(title??"",msg??"");'
new_mac_balloon = old_mac_balloon + '''
    void PlatformSetIcon(string iconPath) => MacTrayPlugin.SetIcon(iconPath);
    void PlatformSetIconFromData(byte[] pngData) => MacTrayPlugin.SetIconFromData(pngData);'''
content = content.replace(old_mac_balloon, new_mac_balloon, 1)

# ── 6. Add stub platform methods for other platforms ─────────────────────
old_else = '    void PlatformShowBalloon(string a,string b,uint c,uint d) { }'
new_else = old_else + '''
    void PlatformSetIcon(string iconPath) { }
    void PlatformSetIconFromData(byte[] pngData) { }'''
content = content.replace(old_else, new_else, 1)

# ── 7. Add public SetIcon methods after ShowBalloonTip public method ──────
old_public = '''    public void ShowBalloonTip(string title, string message, uint iconType=1, uint timeoutMs=5000)
        { if (_initialized) PlatformShowBalloon(title, message, iconType, timeoutMs); }'''
new_public = old_public + '''

    /// <summary>
    /// 设置托盘图标。Win: 传入 .ico 文件路径；Mac: 传入 .png/.icns 路径（18x18 建议）。
    /// 必须在 Initialize() 之后调用。
    /// </summary>
    public void SetIcon(string iconPath)
        { if (_initialized) PlatformSetIcon(iconPath); }

    /// <summary>
    /// 从 Texture2D 内存数据设置托盘图标（仅 Mac 支持；Win 请使用 SetIcon(string)）。
    /// 用法：SetIcon(myTexture2D.EncodeToPNG());
    /// </summary>
    public void SetIcon(byte[] pngData)
        { if (_initialized) PlatformSetIconFromData(pngData); }

    /// <summary>从 Texture2D 直接设置（仅 Mac 支持）。</summary>
    public void SetIcon(UnityEngine.Texture2D texture)
    {
        if (texture == null || !_initialized) return;
        PlatformSetIconFromData(texture.EncodeToPNG());
    }'''
content = content.replace(old_public, new_public, 1)

open(path, 'w', encoding='utf-8').write(content)
print('TrayIconService.cs patched, chars:', len(content))
