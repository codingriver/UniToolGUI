# -*- coding: utf-8 -*-
import os, shutil

src = r'd:\UniToolGUI\Assets\Plugins'
dst = r'd:\UniToolGUI\Assets\Plugins\NativeKit'

# Files and dirs to move into NativeKit (everything except NativeKit itself)
exclude = {'NativeKit', 'NativeKit.meta'}

os.makedirs(dst, exist_ok=True)

moved = []
for name in os.listdir(src):
    if name in exclude:
        continue
    src_path = os.path.join(src, name)
    dst_path = os.path.join(dst, name)
    shutil.move(src_path, dst_path)
    moved.append(name)
    print('moved:', name)

print(f'Total moved: {len(moved)} items')
