#import <AppKit/AppKit.h>
#import "MacTray.h"
#import <objc/runtime.h>
#import <string.h>

static NSStatusItem* g_statusItem = nil;
static NSMenu* g_menu = nil;
static MacTray_MenuCallback g_menuCallback = NULL;

static void invokeCallback(int index) {
    if (g_menuCallback) {
        g_menuCallback(index);
    }
}

@interface MacTrayDelegate : NSObject <NSMenuDelegate>
@end

@implementation MacTrayDelegate

- (void)menuItemClicked:(NSMenuItem*)sender {
    NSNumber* num = objc_getAssociatedObject(sender, "MacTrayIndex");
    if (num) {
        invokeCallback([num intValue]);
    }
}

@end

static MacTrayDelegate* g_delegate = nil;

int MacTray_Init(void) {
    if (g_statusItem) return 1;
    if (!g_delegate) g_delegate = [[MacTrayDelegate alloc] init];
    
    if ([NSThread isMainThread]) {
        NSStatusBar* bar = [NSStatusBar systemStatusBar];
        g_statusItem = [bar statusItemWithLength:NSVariableStatusItemLength];
        if (g_statusItem) {
            g_statusItem.button.image = [NSImage imageNamed:NSImageNameApplicationIcon];
            g_statusItem.button.toolTip = @"Unity App";
        }
    } else {
        dispatch_sync(dispatch_get_main_queue(), ^{
            NSStatusBar* bar = [NSStatusBar systemStatusBar];
            g_statusItem = [bar statusItemWithLength:NSVariableStatusItemLength];
            if (g_statusItem) {
                g_statusItem.button.image = [NSImage imageNamed:NSImageNameApplicationIcon];
                g_statusItem.button.toolTip = @"Unity App";
            }
        });
    }
    return g_statusItem != nil ? 1 : 0;
}

void MacTray_Shutdown(void) {
    if (!g_statusItem) return;
    void (^block)(void) = ^{
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
    dispatch_async(dispatch_get_main_queue(), ^{
        g_statusItem.button.toolTip = s;
    });
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
                NSMenuItem* item = [[NSMenuItem alloc] initWithTitle:title action:@selector(menuItemClicked:) keyEquivalent:@""];
                item.target = g_delegate;
                item.enabled = YES;
                objc_setAssociatedObject(item, "MacTrayIndex", @(menuIdx), OBJC_ASSOCIATION_RETAIN);
                [g_menu addItem:item];
                menuIdx++;
            }
        }
        g_statusItem.menu = g_menu;
    };
    
    if ([NSThread isMainThread]) {
        block();
    } else {
        dispatch_sync(dispatch_get_main_queue(), block);
    }
}

void MacTray_ShowBalloon(const char* title, const char* message) {
    if (!g_statusItem) return;
    NSString* t = title ? [NSString stringWithUTF8String:title] : @"";
    NSString* m = message ? [NSString stringWithUTF8String:message] : @"";
    void (^block)(void) = ^{
        if (@available(macOS 10.14, *)) {
            // macOS 10.14+: 使用 UNUserNotificationCenter
            Class UNCenter = NSClassFromString(@"UNUserNotificationCenter");
            if (UNCenter) {
                id center = [UNCenter performSelector:@selector(currentNotificationCenter)];
                [center performSelector:@selector(requestAuthorizationWithOptions:completionHandler:)
                             withObject:@(1 | 2) // UNAuthorizationOptionAlert | UNAuthorizationOptionSound
                             withObject:^(BOOL granted, NSError* error) {
                    if (granted) {
                        Class UNContent = NSClassFromString(@"UNMutableNotificationContent");
                        id content = [[UNContent alloc] init];
                        [content performSelector:@selector(setTitle:) withObject:t];
                        [content performSelector:@selector(setBody:) withObject:m];
                        
                        Class UNRequest = NSClassFromString(@"UNNotificationRequest");
                        NSString* identifier = [[NSUUID UUID] UUIDString];
                        id request = [UNRequest performSelector:@selector(requestWithIdentifier:content:trigger:)
                                                     withObject:identifier
                                                     withObject:content
                                                     withObject:nil];
                        [center performSelector:@selector(addNotificationRequest:withCompletionHandler:)
                                     withObject:request
                                     withObject:nil];
                    }
                }];
                return;
            }
        }
        // macOS 10.13 及以下回退到 NSUserNotification
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
        if ([NSUserNotificationCenter class]) {
            NSUserNotification* notif = [[NSUserNotification alloc] init];
            notif.title = t;
            notif.informativeText = m;
            [[NSUserNotificationCenter defaultUserNotificationCenter] deliverNotification:notif];
        }
#pragma clang diagnostic pop
    };
    if ([NSThread isMainThread]) block();
    else dispatch_async(dispatch_get_main_queue(), block);
}

void MacTray_SetMenuCallback(MacTray_MenuCallback callback) {
    g_menuCallback = callback;
}

void MacTray_ShowMainWindow(void) {
    void (^block)(void) = ^{
        [[NSApplication sharedApplication] activateIgnoringOtherApps:YES];
        [[NSApplication sharedApplication] arrangeInFront:nil];
    };
    if ([NSThread isMainThread]) block();
    else dispatch_async(dispatch_get_main_queue(), block);
}

// ---------- 窗口位置/大小/置顶 ----------
int MacWindow_GetFrame(int* outX, int* outY, int* outWidth, int* outHeight) {
    if (!outX || !outY || !outWidth || !outHeight) return 0;
    *outX = *outY = *outWidth = *outHeight = 0;
    __block int res = 0;
    void (^block)(void) = ^{
        NSWindow* win = [NSApp mainWindow];
        if (!win) win = [NSApp keyWindow];
        if (!win) return;
        NSRect frame = [win frame];
        NSScreen* screen = [win screen] ?: [NSScreen mainScreen];
        CGFloat screenHeight = screen ? screen.frame.size.height : 600;
        *outX = (int)frame.origin.x;
        *outY = (int)(screenHeight - frame.origin.y - frame.size.height);
        *outWidth = (int)frame.size.width;
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
        NSWindow* win = [NSApp mainWindow];
        if (!win) win = [NSApp keyWindow];
        if (!win) return;
        NSScreen* screen = [win screen] ?: [NSScreen mainScreen];
        CGFloat screenHeight = screen ? screen.frame.size.height : 600;
        CGFloat flippedY = screenHeight - y - height;
        NSRect frame = NSMakeRect((CGFloat)x, flippedY, (CGFloat)width, (CGFloat)height);
        [win setFrame:frame display:YES animate:NO];
        result = 1;
    };
    if ([NSThread isMainThread]) block();
    else dispatch_sync(dispatch_get_main_queue(), block);
    return result;
}

int MacWindow_SetTopMost(int topMost) {
    __block int result = 0;
    void (^block)(void) = ^{
        NSWindow* win = [NSApp mainWindow];
        if (!win) win = [NSApp keyWindow];
        if (!win) return;
        [win setLevel: topMost ? NSFloatingWindowLevel : NSNormalWindowLevel];
        result = 1;
    };
    if ([NSThread isMainThread]) block();
    else dispatch_sync(dispatch_get_main_queue(), block);
    return result;
}
