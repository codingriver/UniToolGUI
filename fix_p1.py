lines = open(r'd:\UniToolGUI\api_p1.py', encoding='utf-8').readlines()
lines[214] = "w()\n"
open(r'd:\UniToolGUI\api_p1.py', 'w', encoding='utf-8').writelines(lines)
print('fixed line 215:', repr(lines[214]))
