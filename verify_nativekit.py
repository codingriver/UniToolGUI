import os
root = r'd:\UniToolGUI\Assets\Plugins\NativeKit'
count = 0
for dirpath, dirs, files in os.walk(root):
    level = dirpath.replace(root, '').count(os.sep)
    indent = '  ' * level
    print(f'{indent}{os.path.basename(dirpath)}/')
    for f in sorted(files):
        if not f.endswith('.meta'):
            print(f'{indent}  {f}')
            count += 1
print(f'Total non-meta files: {count}')
