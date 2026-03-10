#!/usr/bin/env python3
"""Fix garbled Chinese text in C# files caused by PowerShell encoding corruption.

Two types of damage:
1. Truncated chars: last Chinese char lost its final byte -> shows as partial char + ?
2. Line merges: comment line merged with next code line (newline was part of truncated byte)
"""

import os
import re
import codecs

BASE = r"D:\ProxyTool\Assets\Scripts\Core"

# ============================================================
# SIMPLE REPLACEMENTS: garbled -> correct
# These fix truncated characters (last byte lost)
# ============================================================
REPLACEMENTS = [
    # Common word endings
    ("状\ufffd?", "状态"),    ("状?", "状态"),
    ("工\ufffd?", "工具"),    ("工?", "工具"),
    ("变\ufffd?", "变量"),    ("变?", "变量"),
    ("应\ufffd?", "应用"),    ("应?", "应用"),
    ("清\ufffd?", "清除"),    ("清?", "清除"),
    ("设\ufffd?", "设置"),    ("设?", "设置"),
    ("跳\ufffd?", "跳过"),    ("跳?", "跳过"),
    ("存\ufffd?", "存在"),    ("存?", "存在"),
    ("代\ufffd?", "代理"),    ("代?", "代理"),
    ("禁\ufffd?", "禁用"),    ("禁?", "禁用"),
    ("启\ufffd?", "启用"),    ("启?", "启用"),    ("启\ufffd?", "启动"),
    ("配\ufffd?", "配置"),    ("配?", "配置"),
    ("管\ufffd?", "管理"),    ("管?", "管理"),
    ("检\ufffd?", "检测"),    ("检?", "检测"),
    ("路\ufffd?", "路径"),    ("路?", "路径"),
    ("枚\ufffd?", "枚举"),    ("枚?", "枚举"),
    ("参\ufffd?", "参数"),    ("参?", "参数"),
    ("可\ufffd?", "可用"),    ("可?", "可用"),
    ("解\ufffd?", "解析"),    ("解?", "解析"),
    ("返\ufffd?", "返回"),    ("返?", "返回"),
    ("替\ufffd?", "替代"),    ("替?", "替代"),
    ("生\ufffd?", "生效"),    ("生?", "生效"),
    ("概\ufffd?", "概念"),    ("概?", "概念"),
    ("注\ufffd?", "注册"),    ("注?", "注册"),
    ("分\ufffd?", "分割"),    ("分?", "分割"),
    ("退\ufffd?", "退出"),    ("退?", "退出"),
    ("找\ufffd?", "找到"),    ("找?", "找到"),
    ("消\ufffd?", "消除"),    ("消?", "消除"),
    ("实\ufffd?", "实现"),    ("实?", "实现"),
    ("效\ufffd?", "效果"),    ("效?", "效果"),
    ("需\ufffd?", "需要"),    ("需?", "需要"),
    ("载\ufffd?", "载器"),    ("载?", "载器"),
    ("取\ufffd?", "取消"),    ("取?", "取消"),
    ("测\ufffd?", "测试"),    ("测?", "测试"),
    ("称\ufffd?", "称为"),    ("称?", "称为"),
    ("端\ufffd?", "端口"),    ("端?", "端口"),
    ("支\ufffd?", "支持"),    ("支?", "支持"),
    ("段\ufffd?", "段落"),    ("段?", "段落"),
    ("项\ufffd?", "项目"),    ("项?", "项目"),
    ("文\ufffd?", "文件"),    ("文?", "文件"),
    ("器\ufffd?", "器）"),    ("器?", "器）"),  # handler -> 处理器）
    ("表\ufffd?", "表）"),    ("表?", "表）"),
    ("空\ufffd?", "空行"),    ("空?", "空行"),
    ("用\ufffd?", "用："),    ("用?", "用："),
    ("境\ufffd?", "境）"),    ("境?", "境）"),
    ("果\ufffd?", "果）"),    ("果?", "果）"),
    
    # Specific patterns
    ("处理\ufffd?", "处理器"),
    ("编辑\ufffd?", "编辑器"),
    ("注册\ufffd?", "注册表"),
    ("浏览\ufffd?", "浏览器"),
    ("系统\ufffd?", "系统级"),
    ("连通\ufffd?", "连通性"),
    ("容器\ufffd?", "容器）"),
    ("环境\ufffd?", "环境）"),
    ("使用\ufffd?", "使用）"),
    ("命令\ufffd?", "命令）"),
    ("可执行文\ufffd?", "可执行文件"),
    ("加载\ufffd?", "加载器"),
    ("包管\ufffd?", "包管理"),
    ("顺序\ufffd?", "顺序）"),
    ("通\ufffd?", "通性"),
    
    # Em dash replacements
    (" \ufffd?~/", " — ~/"),
    (" \ufffd?%", " — %"),
    (" \ufffd?OS", " — OS"),
    (" \ufffd?通过", " — 通过"),
    
    # Special chars
    ("\ufffd?成功", "✓ 成功"),
    ("\ufffd?失败", "✗ 失败"),
    ("\ufffd?可用", "✓ 可用"),
    ("\ufffd?不可", "✗ 不可"),
    ("\ufffd?已启", "✓ 已启"),
    ("\ufffd?Windows", "仅 Windows"),  # [仅Windows] -> 仅 was [\ufffd?Windows]
]


def fix_file(filepath):
    """Fix garbled Chinese text in a single file."""
    with open(filepath, 'r', encoding='utf-8-sig') as f:
        content = f.read()
    
    original = content
    
    # Apply all simple replacements
    for garbled, correct in REPLACEMENTS:
        content = content.replace(garbled, correct)
    
    # Fix remaining ? after Chinese chars that are clearly truncated
    # Pattern: Chinese char followed by ? where ? is not part of ternary or null-conditional
    # This is context-dependent, so we do it carefully
    
    if content != original:
        # Write back with UTF-8 BOM
        with open(filepath, 'w', encoding='utf-8-sig') as f:
            f.write(content)
        return True
    return False


def main():
    fixed_count = 0
    for root, dirs, files in os.walk(BASE):
        for fname in files:
            if not fname.endswith('.cs'):
                continue
            fpath = os.path.join(root, fname)
            if fix_file(fpath):
                print(f"FIXED: {fname}")
                fixed_count += 1
            else:
                print(f"SKIP:  {fname}")
    
    print(f"\nFixed {fixed_count} files")


if __name__ == '__main__':
    main()
