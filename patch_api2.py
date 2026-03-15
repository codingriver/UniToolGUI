# -*- coding: utf-8 -*-
path = r'd:\UniToolGUI\Assets\Plugins\API.md'
lines = open(path, encoding='utf-8').readlines()

# Find section 1 line
sec1_start = None
for i, l in enumerate(lines):
    if '## 1. TrayIconService' in l:
        sec1_start = i
        break

if sec1_start is None:
    print('Section 1 not found'); exit(1)

# Find next --- after section 1
sec1_end = None
for i in range(sec1_start+1, len(lines)):
    if lines[i].strip() == '---':
        sec1_end = i
        break

if sec1_end is None:
    print('Section end not found'); exit(1)

print(f'Section 1: lines {sec1_start+1} to {sec1_end+1}')

# Check if SetIcon already documented
already = any('SetIcon' in lines[i] for i in range(sec1_start, sec1_end))
if already:
    print('SetIcon already documented')
else:
    # Find the closing ``` of the csharp block in section 1
    insert_at = None
    in_block = False
    for i in range(sec1_start, sec1_end):
        if lines[i].strip().startswith('```csharp'):
            in_block = True
        elif in_block and lines[i].strip() == '```':
            insert_at = i  # insert before closing ```
            in_block = False
            break
    if insert_at is None:
        # append before ---
        insert_at = sec1_end
    
    new_lines = [
        'void SetIcon(string iconPath);   // Win:.ico路径 | Mac:.png/.icns路径(建议18x18)\n',
        'void SetIcon(byte[] pngData);    // 内存PNG设置图标（仅Mac）\n',
        'void SetIcon(Texture2D texture); // Texture2D设置图标（仅Mac，内部EncodeToPNG）\n',
    ]
    for j, nl in enumerate(new_lines):
        lines.insert(insert_at + j, nl)
    
    # Add icon notes after TrayMenuItem line
    for i in range(sec1_start, sec1_end + len(new_lines)):
        if 'TrayMenuItem' in lines[i] and 'Text' in lines[i]:
            note = [
                '\n',
                '**图标说明（v2新增）**：\n',
                '- Windows：仅支持 `.ico` 文件路径\n',
                '- macOS：支持 `.png`/`.icns` 路径和内存PNG；自动缩放18x18，template=YES适配深浅色\n',
                '- `SetIcon()` 必须在 `Initialize()` 之后调用\n',
            ]
            for k, nl in enumerate(note):
                lines.insert(i + 1 + k, nl)
            break
    
    print('SetIcon docs inserted')

# Update v2 log if present
for i, l in enumerate(lines):
    if 'TrayIconService.cs' in l and '三份重复代码' in l and 'SetIcon' not in l:
        lines[i] = l.replace(
            '单文件 #if，公共 API 提顶层',
            '单文件 #if；新增 SetIcon(path/bytes/Texture2D)'
        )
        print(f'Updated v2 log at line {i+1}')
        break

for i, l in enumerate(lines):
    if 'MacTray.h/m' in l and 'SetIcon' not in l:
        lines[i] = l.replace(
            '新增 6 函数，dispatch_sync 安全',
            '新增 6 窗口函数 + MacTray_SetIcon/SetIconFromData'
        )
        print(f'Updated MacTray log at line {i+1}')
        break

open(path, 'w', encoding='utf-8').writelines(lines)
print('Done, total lines:', len(lines))
