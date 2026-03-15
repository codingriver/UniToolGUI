# -*- coding: utf-8 -*-
insert_text = '''
// ── 托盘图标自定义 ──────────────────────────────────────

static void ApplyTrayImage(NSImage* img) {
    if (!img || !g_statusItem) return;
    NSImage* scaled = [[NSImage alloc] initWithSize:NSMakeSize(18, 18)];
    [scaled lockFocus];
    [img drawInRect:NSMakeRect(0, 0, 18, 18)
           fromRect:NSZeroRect
          operation:NSCompositingOperationSourceOver
           fraction:1.0];
    [scaled unlockFocus];
    scaled.template = YES;
    void (^block)(void) = ^{ g_statusItem.button.image = scaled; };
    if ([NSThread isMainThread]) block();
    else dispatch_sync(dispatch_get_main_queue(), block);
}

int MacTray_SetIcon(const char* imagePath) {
    if (!imagePath) return 0;
    NSString* path = [NSString stringWithUTF8String:imagePath];
    NSImage* img = [[NSImage alloc] initWithContentsOfFile:path];
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
'''

path = r'd:\UniToolGUI\Assets\Plugins\MacOS\MacTray\MacTray.m'
lines = open(path, encoding='utf-8').readlines()

# Find line index of MacTray_SetMenuCallback closing brace
insert_after = -1
for i, line in enumerate(lines):
    if 'MacTray_SetMenuCallback' in line:
        # find the closing }
        for j in range(i, min(i+10, len(lines))):
            if lines[j].strip() == '}':
                insert_after = j
                break
        break

if insert_after == -1:
    print('ERROR: could not find insertion point')
else:
    lines.insert(insert_after + 1, insert_text)
    open(path, 'w', encoding='utf-8').writelines(lines)
    print(f'Inserted after line {insert_after+1}, total lines now: {len(lines)}')
