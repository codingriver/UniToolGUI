#ifndef MacTray_h
#define MacTray_h

#ifdef __cplusplus
extern "C" {
#endif

// ── 托盘 ──────────────────────────────────────────────────────────────────
int  MacTray_Init(void);
void MacTray_Shutdown(void);
void MacTray_SetTooltip(const char* tooltip);
void MacTray_SetMenu(const char** items);
void MacTray_ShowBalloon(const char* title, const char* message);
typedef void (*MacTray_MenuCallback)(int index);
void MacTray_SetMenuCallback(MacTray_MenuCallback callback);
void MacTray_ShowMainWindow(void);

/// 从文件路径设置托盘图标（支持 PNG / ICNS）。返回 1 成功，0 失败。
/// imagePath: UTF-8 路径，建议图片尺寸 18x18 px。
int MacTray_SetIcon(const char* imagePath);

/// 从内存 PNG 数据设置托盘图标。返回 1 成功，0 失败。
/// pngData: PNG 字节数组；length: 字节长度。
int MacTray_SetIconFromData(const unsigned char* pngData, int length);

// ── 窗口位置/大小/置顶 ────────────────────────────────────────────────────
// 坐标系：左上角为原点（与 Windows 一致，内部翻转 macOS 坐标）
int MacWindow_GetFrame(int* outX, int* outY, int* outWidth, int* outHeight);
int MacWindow_SetFrame(int x, int y, int width, int height);
int MacWindow_SetTopMost(int topMost);

// ── 新增：窗口状态控制 ────────────────────────────────────────────────────
/// 最小化主窗口。返回 1 成功，0 失败。
int MacWindow_Minimize(void);
/// 最大化（Zoom）主窗口。返回 1 成功，0 失败。
int MacWindow_Maximize(void);
/// 还原主窗口（取消最小化或最大化）。返回 1 成功，0 失败。
int MacWindow_Restore(void);
/// 返回 1 表示已最小化，0 否。
int MacWindow_IsMinimized(void);
/// 返回 1 表示已最大化（Zoomed），0 否。
int MacWindow_IsMaximized(void);
/// 设置窗口透明度。alpha: 0.0（完全透明）~ 1.0（完全不透明）。返回 1 成功。
int MacWindow_SetAlpha(float alpha);

#ifdef __cplusplus
}
#endif

#endif /* MacTray_h */
