#!/usr/bin/env python3
"""Build advanced self-compiled test EXEs for the Win32 emulator."""
import sys, struct
sys.path.insert(0, '/tmp')
exec(open('/tmp/pe_factory.py').read())

OUT = '/mnt/e/SteamLibrary/steamapps/common/sbox/data/xenthio/xgui-3_test#local/community_suggestions/'

def lea_eax_esp(n=0):
    if n==0: return b'\x8D\x04\x24'
    if n<=127: return b'\x8D\x44\x24'+bytes([n])
    return b'\x8D\x84\x24'+struct.pack('<I',n)
def lea_ecx_esp(n=0):
    if n==0: return b'\x8D\x0C\x24'
    if n<=127: return b'\x8D\x4C\x24'+bytes([n])
    return b'\x8D\x8C\x24'+struct.pack('<I',n)
def mov_esp_imm(n,v32):
    if n==0: return b'\xC7\x04\x24'+struct.pack('<I',v32)
    if n<=127: return b'\xC7\x44\x24'+bytes([n])+struct.pack('<I',v32)
    return b'\xC7\x84\x24'+struct.pack('<I',n)+struct.pack('<I',v32)
def simple_wndproc(v):
    WM_DESTROY=0x0002
    p =b'\x55\x8B\xEC\x8B\x45\x0C'
    p+=b'\x3D\x02\x00\x00\x00'
    p+=b'\x75\x0B'
    p+=push8(0)+call_iat(v['PostQuitMessage'])
    p+=b'\x31\xC0\x5D\xC2\x10\x00'
    p+=b'\xFF\x75\x14\xFF\x75\x10\xFF\x75\x0C\xFF\x75\x08'
    p+=call_iat(v['DefWindowProcA'])
    p+=b'\x5D\xC2\x10\x00'
    return p
def simple_msg_loop(v):
    e=sub_esp(28)
    loop =lea_eax_esp()+b'\x6A\x00\x6A\x00\x6A\x00\x50'
    loop+=call_iat(v['GetMessageA'])
    loop+=b'\x85\xC0\x74\x14'
    loop+=b'\x54'+call_iat(v['TranslateMessage'])
    loop+=b'\x54'+call_iat(v['DispatchMessageA'])
    loop+=b'\xEB'+bytes([256-len(loop)-2])
    return e+loop+add_esp(28)

# ── EXE 1: clawd_registry.exe ────────────────────────────────────────────────
def build_registry():
    imports={'ADVAPI32.DLL':[('RegCreateKeyExA',0),('RegSetValueExA',0),('RegQueryValueExA',0),('RegCloseKey',0)],'KERNEL32.DLL':[('ExitProcess',0)],'USER32.DLL':[('MessageBoxA',0)]}
    v=resolve_iat_vas(imports)
    strings =b'ClaWd\x00'
    strings+=b'Software\\ClaWdTest\x00'   # 6
    strings+=b'TestValue\x00'              # 25
    strings+=b'Hello42\x00'               # 35
    strings+=b'RegTest OK: value written\x00' # 43
    strings+=b'RegTest FAIL: create\x00'  # 68
    strings+=b'RegTest FAIL: set\x00'     # 89
    strings+=b'RegTest FAIL: query\x00'   # 107
    S_SUBKEY=str_rva(6);S_VALNAME=str_rva(25);S_VALDATA=str_rva(35)
    S_CAP=str_rva(0);S_OK=str_rva(43);S_FCR=str_rva(68);S_FSET=str_rva(89);S_FQRY=str_rva(107)
    HKCU=0x80000001;KEY_ALL=0xF003F;REG_SZ=1
    # Stack: [esi+0]=hKey [esi+4]=dwDisp [esi+8]=cbData(64) [esi+12]=readbuf(64)
    c =sub_esp(80)+mov_esp_imm(8,64)+b'\x89\xE6'  # esi=esp
    # RegCreateKeyExA args (right to left)
    c+=b'\x8D\x46\x04\x50'  # lea eax,[esi+4]; push &dwDisp
    c+=b'\x56'               # push &hKey=esi
    c+=push8(0)+push32(KEY_ALL)+push8(0)+push8(0)+push8(0)
    c+=push32(S_SUBKEY)+push32(HKCU)
    c+=call_iat(v['RegCreateKeyExA'])+b'\x85\xC0'
    jnz_cr=len(c);c+=b'\x75\x00'
    # RegSetValueExA
    c+=b'\x8B\x06'+push8(8)+push32(S_VALDATA)+push32(REG_SZ)+push8(0)+push32(S_VALNAME)+b'\x50'
    c+=call_iat(v['RegSetValueExA'])+b'\x85\xC0'
    jnz_set=len(c);c+=b'\x75\x00'
    # RegQueryValueExA
    c+=b'\x8B\x06\x8D\x5E\x0C\x8D\x4E\x08\x51\x53'+push8(0)+push8(0)+push32(S_VALNAME)+b'\x50'
    c+=call_iat(v['RegQueryValueExA'])+b'\x85\xC0'
    jnz_qry=len(c);c+=b'\x75\x00'
    # Verify 'H' at [esi+12]
    c+=b'\x80\x7E\x0C\x48'
    jne_qry=len(c);c+=b'\x75\x00'
    # RegCloseKey + success
    c+=b'\x8B\x06\x50'+call_iat(v['RegCloseKey'])
    c+=push8(0)+push32(S_CAP)+push32(S_OK)+push8(0)+call_iat(v['MessageBoxA'])
    jmp_ex=len(c);c+=b'\xEB\x00'
    fcr=len(c);c+=push8(0)+push32(S_CAP)+push32(S_FCR)+push8(0)+call_iat(v['MessageBoxA']);j2=len(c);c+=b'\xEB\x00'
    fset=len(c);c+=push8(0)+push32(S_CAP)+push32(S_FSET)+push8(0)+call_iat(v['MessageBoxA']);j3=len(c);c+=b'\xEB\x00'
    fqry=len(c);c+=push8(0)+push32(S_CAP)+push32(S_FQRY)+push8(0)+call_iat(v['MessageBoxA'])
    ex=len(c);c+=add_esp(80)+push8(0)+call_iat(v['ExitProcess'])+int3()
    c=bytearray(c)
    c[jnz_cr+1]=(fcr-jnz_cr-2)&0xFF;c[jnz_set+1]=(fset-jnz_set-2)&0xFF
    c[jnz_qry+1]=(fqry-jnz_qry-2)&0xFF;c[jne_qry+1]=(fqry-jne_qry-2)&0xFF
    c[jmp_ex+1]=(ex-jmp_ex-2)&0xFF;c[j2+1]=(ex-j2-2)&0xFF;c[j3+1]=(ex-j3-2)&0xFF
    pe=build_pe(bytes(c),imports,strings)
    open(OUT+'clawd_registry.exe','wb').write(pe)
    print(f'Built clawd_registry.exe: {len(pe)} bytes')

# ── EXE 2: clawd_childwnd.exe ────────────────────────────────────────────────
def build_childwnd():
    imports={'KERNEL32.DLL':[('ExitProcess',0)],'USER32.DLL':[('RegisterClassExA',0),('CreateWindowExA',0),('ShowWindow',0),('UpdateWindow',0),('GetMessageA',0),('TranslateMessage',0),('DispatchMessageA',0),('DefWindowProcA',0),('PostQuitMessage',0),('MessageBoxA',0)]}
    v=resolve_iat_vas(imports)
    strings =b'ClaWdChild\x00'       # 0
    strings+=b'Parent Window\x00'    # 11
    strings+=b'Child1\x00'           # 25
    strings+=b'Child2\x00'           # 32
    strings+=b'Child3\x00'           # 39
    strings+=b'3 children OK\x00'    # 46
    S_CLASS=str_rva(0);S_PAR=str_rva(11);S_C1=str_rva(25);S_C2=str_rva(32);S_C3=str_rva(39)
    S_OK=str_rva(46);S_CAP=str_rva(0)
    WP=rva_to_va(TEXT_RVA+0x150);WS_OV=0x00CF0000|0x10000000;WS_CH=0x40000000|0x10000000
    e=sub_esp(48)
    for off in range(0,48,4): e+=b'\xC7\x44\x24'+bytes([off])+b'\x00\x00\x00\x00'
    e+=mov_esp_imm(0,48)+mov_esp_imm(4,3)+mov_esp_imm(8,WP)+mov_esp_imm(0x28,S_CLASS)
    e+=b'\x54'+call_iat(v['RegisterClassExA'])
    e+=push8(0)+push8(0)+push8(0)+push8(0)+push32(300)+push32(400)+push32(50)+push32(50)
    e+=push32(WS_OV)+push32(S_PAR)+push32(S_CLASS)+push8(0)+call_iat(v['CreateWindowExA'])
    e+=b'\x89\xC6'  # mov esi,eax (parent)
    for i,st in enumerate([S_C1,S_C2,S_C3]):
        e+=push8(0)+push8(0)+push32(i+1)+b'\x56'
        e+=push32(50)+push32(80)+push32(i*90+10)+push32(10)
        e+=push32(WS_CH)+push32(st)+push32(S_CLASS)+push8(0)+call_iat(v['CreateWindowExA'])
    e+=push8(5)+b'\x56'+call_iat(v['ShowWindow'])+b'\x56'+call_iat(v['UpdateWindow'])
    e+=push8(0)+push32(S_CAP)+push32(S_OK)+push8(0)+call_iat(v['MessageBoxA'])
    e+=simple_msg_loop(v)+add_esp(48)+push8(0)+call_iat(v['ExitProcess'])+int3()
    text=bytes(e).ljust(0x150,b'\x90')+simple_wndproc(v)
    pe=build_pe(text,imports,strings)
    open(OUT+'clawd_childwnd.exe','wb').write(pe)
    print(f'Built clawd_childwnd.exe: {len(pe)} bytes')

# ── EXE 3: clawd_heap.exe ─────────────────────────────────────────────────────
def build_heap():
    imports={'KERNEL32.DLL':[('ExitProcess',0),('GetProcessHeap',0),('HeapAlloc',0),('HeapFree',0),('VirtualAlloc',0),('VirtualFree',0)],'USER32.DLL':[('MessageBoxA',0)]}
    v=resolve_iat_vas(imports)
    strings=b'ClaWd\x00'+b'Heap+VAlloc OK\x00'+b'HeapAlloc FAIL\x00'+b'VirtualAlloc FAIL\x00'
    c=call_iat(v['GetProcessHeap'])+b'\x89\xC3'
    c+=push32(1024)+push32(8)+b'\x53'+call_iat(v['HeapAlloc'])+b'\x85\xC0'
    jzh=len(c);c+=b'\x74\x00'
    c+=b'\xC7\x00\xEF\xBE\xAD\xDE\x89\xC6\x56'+push8(0)+b'\x53'+call_iat(v['HeapFree'])
    c+=push32(4)+push32(0x1000)+push32(4096)+push8(0)+call_iat(v['VirtualAlloc'])+b'\x85\xC0'
    jzv=len(c);c+=b'\x74\x00'
    c+=b'\xC7\x00\xBE\xBA\xFE\xCA\x89\xC6'+push32(0x8000)+push8(0)+b'\x56'+call_iat(v['VirtualFree'])
    c+=push8(0)+push32(str_rva(0))+push32(str_rva(6))+push8(0)+call_iat(v['MessageBoxA'])
    je=len(c);c+=b'\xEB\x00'
    hf=len(c);c+=push8(0)+push32(str_rva(0))+push32(str_rva(21))+push8(0)+call_iat(v['MessageBoxA']);j2=len(c);c+=b'\xEB\x00'
    vf=len(c);c+=push8(0)+push32(str_rva(0))+push32(str_rva(36))+push8(0)+call_iat(v['MessageBoxA'])
    ex=len(c);c+=push8(0)+call_iat(v['ExitProcess'])+int3()
    c=bytearray(c);c[jzh+1]=(hf-jzh-2)&0xFF;c[jzv+1]=(vf-jzv-2)&0xFF;c[je+1]=(ex-je-2)&0xFF;c[j2+1]=(ex-j2-2)&0xFF
    pe=build_pe(bytes(c),imports,strings)
    open(OUT+'clawd_heap.exe','wb').write(pe)
    print(f'Built clawd_heap.exe: {len(pe)} bytes')

# ── EXE 4: clawd_wsprintfa.exe ───────────────────────────────────────────────
def build_wsprintfa():
    imports={'KERNEL32.DLL':[('ExitProcess',0),('GetCommandLineA',0),('lstrlenA',0)],'USER32.DLL':[('wsprintfA',0),('MessageBoxA',0)]}
    v=resolve_iat_vas(imports)
    strings=b'ClaWd\x00'+b'Val=%d hex=0x%X\x00'+b'Source\x00'
    c=sub_esp(64)+b'\x89\xE3'
    c+=push32(42)+push32(42)+push32(str_rva(6))+b'\x53'+call_iat(v['wsprintfA'])+add_esp(16)
    c+=push8(0)+push32(str_rva(0))+b'\x53'+push8(0)+call_iat(v['MessageBoxA'])
    c+=push32(str_rva(22))+call_iat(v['lstrlenA'])
    c+=call_iat(v['GetCommandLineA'])
    c+=add_esp(64)+push8(0)+call_iat(v['ExitProcess'])+int3()
    pe=build_pe(bytes(c),imports,strings)
    open(OUT+'clawd_wsprintfa.exe','wb').write(pe)
    print(f'Built clawd_wsprintfa.exe: {len(pe)} bytes')

# ── EXE 5: clawd_repstring.exe ───────────────────────────────────────────────
def build_repstring():
    imports={'KERNEL32.DLL':[('ExitProcess',0)],'USER32.DLL':[('MessageBoxA',0)]}
    v=resolve_iat_vas(imports)
    strings=b'ClaWd\x00'+b'Hello REP World!\x00'+b'REP OK\x00'+b'REP FAIL\x00'
    c=sub_esp(32)+b'\x89\xE7'+b'\xBE'+struct.pack('<I',str_rva(6))+b'\xB9\x04\x00\x00\x00\xFC\xF3\xA5'
    c+=b'\x81\x3C\x24\x48\x65\x6C\x6C'
    jne=len(c);c+=b'\x75\x00'
    c+=push8(0)+push32(str_rva(0))+push32(str_rva(22))+push8(0)+call_iat(v['MessageBoxA'])
    je=len(c);c+=b'\xEB\x00'
    ff=len(c);c+=push8(0)+push32(str_rva(0))+push32(str_rva(29))+push8(0)+call_iat(v['MessageBoxA'])
    ex=len(c);c+=add_esp(32)+push8(0)+call_iat(v['ExitProcess'])+int3()
    c=bytearray(c);c[jne+1]=(ff-jne-2)&0xFF;c[je+1]=(ex-je-2)&0xFF
    pe=build_pe(bytes(c),imports,strings)
    open(OUT+'clawd_repstring.exe','wb').write(pe)
    print(f'Built clawd_repstring.exe: {len(pe)} bytes')

# ── EXE 6: clawd_exception.exe ───────────────────────────────────────────────
def build_exception():
    imports={'KERNEL32.DLL':[('ExitProcess',0)],'USER32.DLL':[('MessageBoxA',0)]}
    v=resolve_iat_vas(imports)
    strings=b'ClaWd\x00'+b'Exception swallowed\x00'+b'No exception (bad)\x00'
    # DIV by zero — emulator should fault, test accepts it as "expected fault"
    c=b'\x31\xC0\x31\xD2\x31\xC9\xF7\xF9'  # xor eax; xor edx; xor ecx; idiv ecx
    # If emulator swallows the exception and continues:
    c+=push8(0)+push32(str_rva(0))+push32(str_rva(6))+push8(0)+call_iat(v['MessageBoxA'])
    c+=push8(0)+call_iat(v['ExitProcess'])+int3()
    pe=build_pe(bytes(c),imports,strings)
    open(OUT+'clawd_exception.exe','wb').write(pe)
    print(f'Built clawd_exception.exe: {len(pe)} bytes')

if __name__=='__main__':
    build_registry()
    build_childwnd()
    build_heap()
    build_wsprintfa()
    build_repstring()
    build_exception()
    print('Done!')
