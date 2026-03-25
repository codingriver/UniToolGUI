#import <AppKit/AppKit.h>
#import <UserNotifications/UserNotifications.h>
#import "MacTray.h"
#import <objc/runtime.h>
#import <string.h>

static NSStatusItem* g_statusItem = nil;
static NSMenu* g_menu = nil;
static MacTray_MenuCallback g_menuCallback = NULL;
static BOOL g_hideOnClose = NO;

// ── NSWindowDelegate proxy ────────────────────────────────────────────────
// Only intercepts windowShouldClose: to hide instead of close.
// Does NOT replace NSApp delegate (that breaks Unity's event loop).

@interface MacTrayWindowDelegate : NSObject <NSWindowDelegate>
@end

@implementation MacTrayWindowDelegate

- (BOOL)windowShouldClose:(NSWindow*)win {
    if (g_hideOnClose) {
        [win orderOut:nil];
        return NO;
    }
    return YES;
}

@end

static MacTrayWindowDelegate* g_winDelegate = nil;

static void invokeCallback(int index) {
    if (g_menuCallback) g_menuCallback(index);
}

@interface MacTrayMenuDelegate : NSObject <NSMenuDelegate>
@end

@implementation MacTrayMenuDelegate

- (void)menuItemClicked:(NSMenuItem*)sender {
    NSNumber* num = objc_getAssociatedObject(sender, "MacTrayIndex");
    if (num) invokeCallback([num intValue]);
}

@end

static MacTrayMenuDelegate* g_delegate = nil;

int MacTray_Init(void) {
    if (g_statusItem) return 1;
    if (!g_delegate)    g_delegate    = [[MacTrayMenuDelegate alloc] init];
    if (!g_winDelegate) g_winDelegate = [[MacTrayWindowDelegate alloc] init];

    void (^block)(void) = ^{
        NSStatusBar* bar = [NSStatusBar systemStatusBar];
        g_statusItem = [bar statusItemWithLength:NSVariableStatusItemLength];
        if (g_statusItem) {
            g_statusItem.button.image = [NSImage imageNamed:NSImageNameApplicationIcon];
            g_statusItem.button.toolTip = @"Unity App";
        }
        // Set window delegate on all existing windows (do NOT touch NSApp delegate)
        for (NSWindow* w in [NSApp windows]) {
            [w setDelegate:g_winDelegate];
        }
    };

    if ([NSThread isMainThread]) block();
    else dispatch_sync(dispatch_get_main_queue(), block);

    return g_statusItem != nil ? 1 : 0;
}

void MacTray_Shutdown(void) {
    if (!g_statusItem) return;
    void (^block)(void) = ^{
        // Remove window delegate
        for (NSWindow* w in [NSApp windows]) {
            if ([w delegate] == g_winDelegate) [w setDelegate:nil];
        }
        [[NSStatusBar systemStatusBar] removeStatusItem:g_statusItem];
        g_statusItem = nil;
        g_menu = nil;
    };
    if ([NSThread isMainThread]) block();
    else dispatch_sync(dispatch_get_main_queue(), block);
}

void MacTray_SetTooltip(const char* tooltip) {
    if (!g_statusItem) return;
    NSString* s = tooltip ? [NSString stringWithUTF8String:tooltip] : @"";
    dispatch_async(dispatch_get_main_queue(), ^{ g_statusItem.button.toolTip = s; });
}

void MacTray_SetMenu(const char** items) {
    if (!g_statusItem || !items) return;
    void (^block)(void) = ^{
        g_menu = [[NSMenu alloc] init];
        g_menu.autoenablesItems = NO;
        int menuIdx = 0;
        for (const char** p = items; *p; p++) {
            if (strcmp(*p, "---") == 0) {
                [g_menu addItem:[NSMenuItem separatorItem]];
            } else {
                NSString* title = [NSString stringWithUTF8String:*p];
                NSMenuItem* item = [[NSMenuItem alloc] initWithTitle:title
                                                             action:@selector(menuItemClicked:)
                                                      keyEquivalent:@""];
                item.target  = g_delegate;
                item.enabled = YES;
                objc_setAssociatedObject(item, "MacTrayIndex", @(menuIdx), OBJC_ASSOCIATION_RETAIN);
                [g_menu addItem:item];
                menuIdx++;
            }
        }
        g_statusItem.menu = g_menu;
    };
    if ([NSThread isMainThread]) block();
    else dispatch_sync(dispatch_get_main_queue(), block);
}

void MacTray_ShowBalloon(const char* title, const char* message) {
    if (!g_statusItem) return;
    NSString* t = title   ? [NSString stringWithUTF8String:title]   : @"";
    NSString* m = message ? [NSString stringWithUTF8String:message] : @"";
    void (^block)(void) = ^{
        if (@available(macOS 10.14, *)) {
            UNUserNotificationCenter* center = [UNUserNotificationCenter currentNotificationCenter];
            [center requestAuthorizationWithOptions:(UNAuthorizationOptionAlert | UNAuthorizationOptionSound)
                                 completionHandler:^(BOOL granted, NSError* error) {
                if (!granted) return;
                UNMutableNotificationContent* content = [[UNMutableNotificationContent alloc] init];
                content.title = t;
                content.body  = m;
                NSString* identifier = [[NSUUID UUID] UUIDString];
                UNNotificationRequest* req = [UNNotificationRequest requestWithIdentifier:identifier
                                                                                   content:content
                                                                                   trigger:nil];
                [center addNotificationRequest:req withCompletionHandler:nil];
            }];
        } else {
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
            NSUserNotification* notif = [[NSUserNotification alloc] init];
            notif.title = t;
            notif.informativeText = m;
            [[NSUserNotificationCenter defaultUserNotificationCenter] deliverNotification:notif];
#pragma clang diagnostic pop
        }
    };
    if ([NSThread isMainThread]) block();
    else dispatch_async(dispatch_get_main_queue(), block);
}

void MacTray_SetMenuCallback(MacTray_MenuCallback callback) {
    g_menuCallback = callback;
}

// Tray icon

static void ApplyTrayImage(NSImage* img) {
    if (!img || !g_statusItem) return;
    NSImage* scaled = [[NSImage alloc] initWithSize:NSMakeSize(18, 18)];
    [scaled lockFocus];
    [img drawInRect:NSMakeRect(0, 0, 18, 18)
           fromRect:NSZeroRect
          operation:NSCompositingOperationSourceOver
           fraction:1.0];
    [scaled unlockFocus];
    // 保持彩色图标显示。若需要 macOS 菜单栏原生单色效果，应传入专门设计的 template PNG。
    scaled.template = NO;
    void (^block)(void) = ^{ g_statusItem.button.image = scaled; };
    if ([NSThread isMainThread]) block();
    else dispatch_sync(dispatch_get_main_queue(), block);
}

int MacTray_SetIcon(const char* imagePath) {
    if (!imagePath) return 0;
    NSImage* img = [[NSImage alloc] initWithContentsOfFile:[NSString stringWithUTF8String:imagePath]];
    if (!img) return 0;
    ApplyTrayImage(img);
    return 1;
}

int MacTray_SetIconFromData(const unsigned char* pngData, int length) {
    if (!pngData || length <= 0) return 0;
    NSData* data = [NSData dataWithBytes:pngData length:(NSUInteger)length];
    NSImage* img = [[NSImage alloc] initWithData:data];
    if (!img) return 0;
    ApplyTrayImage(img);
    return 1;
}

void MacTray_ShowMainWindow(void) {
    void (^block)(void) = ^{
        NSWindow* win = nil;
        for (NSWindow* w in [NSApp windows]) {
            if ([w isKindOfClass:[NSWindow class]] && ![w isSheet]) { win = w; break; }
        }
        if (!win) win = [NSApp mainWindow];
        if (!win) win = [NSApp keyWindow];
        if (win) {
            if ([win isMiniaturized]) [win deminiaturize:nil];
            if (![win isVisible])     [win makeKeyAndOrderFront:nil];
        }
        [[NSApplication sharedApplication] activateIgnoringOtherApps:YES];
    };
    if ([NSThread isMainThread]) block();
    else dispatch_async(dispatch_get_main_queue(), block);
}

void MacTray_SetHideOnClose(int enable) {
    void (^block)(void) = ^{
        g_hideOnClose = (enable != 0);
        // Refresh window delegate on all windows
        for (NSWindow* w in [NSApp windows]) {
            [w setDelegate:g_winDelegate];
        }
    };
    if ([NSThread isMainThread]) block();
    else dispatch_sync(dispatch_get_main_queue(), block);
}

// Window frame / topmost

static NSWindow* GetMainWin(void) {
    NSWindow* win = [NSApp mainWindow];
    if (win) return win;
    win = [NSApp keyWindow];
    if (win) return win;
    for (NSWindow* w in [NSApp windows]) {
        if ([w isKindOfClass:[NSWindow class]] && ![w isSheet]) return w;
    }
    return nil;
}

int MacWindow_GetFrame(int* outX, int* outY, int* outWidth, int* outHeight) {
    if (!outX || !outY || !outWidth || !outHeight) return 0;
    *outX = *outY = *outWidth = *outHeight = 0;
    __block int res = 0;
    void (^block)(void) = ^{
        NSWindow* win = GetMainWin();
        if (!win) return;
        NSRect frame = [win frame];
        NSScreen* screen = [win screen] ?: [NSScreen mainScreen];
        // Use visibleFrame to get logical-point coordinates
        CGFloat screenH = screen.frame.size.height;
        *outX      = (int)frame.origin.x;
        *outY      = (int)(screenH - frame.origin.y - frame.size.height);
        *outWidth  = (int)frame.size.width;
        *outHeight = (int)frame.size.height;
        res = 1;
    };
    if ([NSThread isMainThread]) block();
    else dispatch_sync(dispatch_get_main_queue(), block);
    return res;
}

int MacWindow_SetFrame(int x, int y, int width, int height) {
    __block int result = 0;
    void (^block)(void) = ^{
        NSWindow* win = GetMainWin();
        if (!win) return;
        NSScreen* screen = [win screen] ?: [NSScreen mainScreen];
        // screen.frame is in logical points (not physical pixels)
        CGFloat screenH = screen.frame.size.height;
        // Clamp to screen bounds
        CGFloat fw = MIN((CGFloat)width,  screen.visibleFrame.size.width);
        CGFloat fh = MIN((CGFloat)height, screen.visibleFrame.size.height);
        CGFloat fx = (CGFloat)x;
        CGFloat fy = screenH - (CGFloat)y - fh;  // flip y (Cocoa origin = bottom-left)
        [win setFrame:NSMakeRect(fx, fy, fw, fh) display:YES animate:NO];
        result = 1;
    };
    if ([NSThread isMainThread]) block();
    else dispatch_sync(dispatch_get_main_queue(), block);
    return result;
}

int MacWindow_SetTopMost(int topMost) {
    __block int result = 0;
    void (^block)(void) = ^{
        NSWindow* win = GetMainWin();
        if (!win) return;
        [win setLevel:topMost ? NSFloatingWindowLevel : NSNormalWindowLevel];
        result = 1;
    };
    if ([NSThread isMainThread]) block();
    else dispatch_sync(dispatch_get_main_queue(), block);
    return result;
}

int MacWindow_Minimize(void) {
    __block int r = 0;
    void (^b)(void) = ^{ NSWindow* w = GetMainWin(); if (!w) return; [w miniaturize:nil]; r = 1; };
    if ([NSThread isMainThread]) b(); else dispatch_sync(dispatch_get_main_queue(), b);
    return r;
}

int MacWindow_Maximize(void) {
    __block int r = 0;
    void (^b)(void) = ^{ NSWindow* w = GetMainWin(); if (!w) return; if (![w isZoomed]) [w zoom:nil]; r = 1; };
    if ([NSThread isMainThread]) b(); else dispatch_sync(dispatch_get_main_queue(), b);
    return r;
}

int MacWindow_Restore(void) {
    __block int r = 0;
    void (^b)(void) = ^{
        NSWindow* w = GetMainWin(); if (!w) return;
        if ([w isMiniaturized]) { [w deminiaturize:nil]; r = 1; }
        else if ([w isZoomed])  { [w zoom:nil];          r = 1; }
    };
    if ([NSThread isMainThread]) b(); else dispatch_sync(dispatch_get_main_queue(), b);
    return r;
}

int MacWindow_IsMinimized(void) {
    __block int r = 0;
    void (^b)(void) = ^{ NSWindow* w = GetMainWin(); if (w) r = [w isMiniaturized] ? 1 : 0; };
    if ([NSThread isMainThread]) b(); else dispatch_sync(dispatch_get_main_queue(), b);
    return r;
}

int MacWindow_IsMaximized(void) {
    __block int r = 0;
    void (^b)(void) = ^{ NSWindow* w = GetMainWin(); if (w) r = [w isZoomed] ? 1 : 0; };
    if ([NSThread isMainThread]) b(); else dispatch_sync(dispatch_get_main_queue(), b);
    return r;
}

int MacWindow_SetAlpha(float alpha) {
    if (alpha < 0.0f) alpha = 0.0f;
    if (alpha > 1.0f) alpha = 1.0f;
    __block int r = 0;
    void (^b)(void) = ^{
        NSWindow* w = GetMainWin(); if (!w) return;
        [w setAlphaValue:(CGFloat)alpha];
        [w setOpaque:(alpha >= 1.0f)];
        r = 1;
    };
    if ([NSThread isMainThread]) b(); else dispatch_sync(dispatch_get_main_queue(), b);
    return r;
}
