#ifndef MacTray_h
#define MacTray_h

#ifdef __cplusplus
extern "C" {
#endif

// 初始化托盘，返回 1 成功，0 失败
int MacTray_Init(void);

// 关闭托盘
void MacTray_Shutdown(void);

// 设置悬停提示
void MacTray_SetTooltip(const char* tooltip);

// 设置菜单项
// items: 字符串数组，每项格式 "text" 或 "---" 表示分隔符，以 NULL 结尾
// 例如: ["显示窗口", "---", "退出", NULL]
void MacTray_SetMenu(const char** items);

// 显示气泡通知（macOS 10.9+）
void MacTray_ShowBalloon(const char* title, const char* message);

// 菜单点击回调：index 为菜单项索引（从 0 开始，分隔符不计入）
typedef void (*MacTray_MenuCallback)(int index);
void MacTray_SetMenuCallback(MacTray_MenuCallback callback);

// 显示主窗口（激活 Unity 窗口）
void MacTray_ShowMainWindow(void);

// ---------- 窗口位置/大小/置顶 ----------
// 获取主窗口 frame，坐标与 Windows 一致（左上角为原点）
// 返回 1 成功，0 失败
int MacWindow_GetFrame(int* outX, int* outY, int* outWidth, int* outHeight);

// 设置主窗口 frame
int MacWindow_SetFrame(int x, int y, int width, int height);

// 设置窗口置顶，topMost=1 置顶，0 取消
int MacWindow_SetTopMost(int topMost);

#ifdef __cplusplus
}
#endif

#endif /* MacTray_h */
