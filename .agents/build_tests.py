#!/usr/bin/env python3
import sys, struct
sys.path.insert(0,'/tmp')
exec(open('/tmp/pe_factory.py').read())

OUT = '/mnt/e/SteamLibrary/steamapps/common/sbox/data/xenthio/xgui-3_test#local/community_suggestions/'

# IAT slot VAs -- DLLs sorted alphabetically: GDI32, KERNEL32, USER32
# GDI32 slots 0-5, null at 6 | KERNEL32 slots 7-8, null at 9 | USER32 slots 10+
BASE_IAT = IMAGE_BASE + RDATA_RVA + 0x300
def IAT(s): return BASE_IAT + s*4
# GDI32 (alpha first)
VA_TEXTOUTA         = IAT(0)
VA_SETBKMODE        = IAT(1)
VA_SETTEXTCOLOR     = IAT(2)
VA_GETSTOCKOBJECT   = IAT(3)
VA_SELECTOBJECT     = IAT(4)
VA_CREATESOLIDBR    = IAT(5)
# null at 6
# KERNEL32
VA_EXITPROCESS      = IAT(7)
VA_GETMODULEHANDLEA = IAT(8)
# null at 9
# USER32
VA_MESSAGEBOXA      = IAT(10)
VA_CREATEWINDOWEXA  = IAT(11)
VA_REGISTERCLASSEXA = IAT(12)
VA_DEFWINDOWPROCA   = IAT(13)
VA_GETMESSAGEA      = IAT(14)
VA_TRANSLATEMSG     = IAT(15)
VA_DISPATCHMSG      = IAT(16)
VA_POSTQUITMSG      = IAT(17)
VA_GETDC            = IAT(18)
VA_RELEASEDC        = IAT(19)
VA_GETCLIENTRECT    = IAT(20)
VA_BEGINPAINT       = IAT(21)
VA_ENDPAINT         = IAT(22)
VA_SHOWWINDOW       = IAT(23)
VA_UPDATEWINDOW     = IAT(24)
VA_INVALIDATERECT   = IAT(25)
VA_FILLRECT         = IAT(26)
VA_DRAWTEXTA        = IAT(27)
VA_DRAWFRAMECONTROL = IAT(28)
VA_DRAWEDGE         = IAT(29)
IMPORTS = {
    'KERNEL32.DLL': [
        ('ExitProcess',       VA_EXITPROCESS),
        ('GetModuleHandleA',  VA_GETMODULEHANDLEA),
    ],
    'USER32.DLL': [
        ('MessageBoxA',       VA_MESSAGEBOXA),
        ('CreateWindowExA',   VA_CREATEWINDOWEXA),
        ('RegisterClassExA',  VA_REGISTERCLASSEXA),
        ('DefWindowProcA',    VA_DEFWINDOWPROCA),
        ('GetMessageA',       VA_GETMESSAGEA),
        ('TranslateMessage',  VA_TRANSLATEMSG),
        ('DispatchMessageA',  VA_DISPATCHMSG),
        ('PostQuitMessage',   VA_POSTQUITMSG),
        ('GetDC',             VA_GETDC),
        ('ReleaseDC',         VA_RELEASEDC),
        ('GetClientRect',     VA_GETCLIENTRECT),
        ('BeginPaint',        VA_BEGINPAINT),
        ('EndPaint',          VA_ENDPAINT),
        ('ShowWindow',        VA_SHOWWINDOW),
        ('UpdateWindow',      VA_UPDATEWINDOW),
        ('InvalidateRect',    VA_INVALIDATERECT),
        ('FillRect',          VA_FILLRECT),
        ('DrawTextA',         VA_DRAWTEXTA),
        ('DrawFrameControl',  VA_DRAWFRAMECONTROL),
        ('DrawEdge',          VA_DRAWEDGE),
    ],
    'GDI32.DLL': [
        ('TextOutA',          VA_TEXTOUTA),
        ('SetBkMode',         VA_SETBKMODE),
        ('SetTextColor',      VA_SETTEXTCOLOR),
        ('GetStockObject',    VA_GETSTOCKOBJECT),
        ('SelectObject',      VA_SELECTOBJECT),
        ('CreateSolidBrush',  VA_CREATESOLIDBR),
    ],
}

# ── TEST 1: Three MessageBoxes in a row ──────────────────────
def build_msgbox_loop():
    strings  = b'Clawd\x00'                                   # 0
    strings += b'[1/3] Hello World!\x00'                      # 6
    strings += b'[2/3] Arithmetic: 6*7=42\x00'               # 25
    strings += b'[3/3] Loop done, ExitProcess next.\x00'      # 50
    S_CAP = str_rva(0)
    S_T1  = str_rva(6)
    S_T2  = str_rva(25)
    S_T3  = str_rva(50)
    code = b''
    for txt in [S_T1, S_T2, S_T3]:
        code += push8(0) + push32(S_CAP) + push32(txt) + push8(0)
        code += call_iat(VA_MESSAGEBOXA)
    code += push8(0) + call_iat(VA_EXITPROCESS) + int3()
    return code, strings

# ── TEST 2: Window + message loop + WM_PAINT ─────────────────
def build_window():
    strs  = b'ClaWdClass\x00'             # 0
    strs += b'Clawd Window Test\x00'      # 11
    strs += b'Hello from Clawd!\x00'      # 29
    strs += b'Clawd\x00'                  # 47

    S_CLASS   = str_rva(0)
    S_TITLE   = str_rva(11)
    S_TEXT    = str_rva(29)
    WM_DESTROY = 0x0002
    WM_PAINT   = 0x000F
    WS_OL_VIS  = 0x00CF0000 | 0x10000000
    WNDPROC_VA = rva_to_va(TEXT_RVA + 0x110)

    # Entry: build WNDCLASSEX on stack (48 bytes), register, create, show, pump
    e = sub_esp(48)
    # zero WNDCLASSEX
    for off in range(0, 48, 4):
        e += b'\xC7\x44\x24' + bytes([off]) + b'\x00\x00\x00\x00'
    # cbSize=48
    e += b'\xC7\x04\x24' + struct.pack('<I', 48)
    # style=3
    e += b'\xC7\x44\x24\x04' + struct.pack('<I', 3)
    # lpfnWndProc
    e += b'\xC7\x44\x24\x08' + struct.pack('<I', WNDPROC_VA)
    # lpszClassName
    e += b'\xC7\x44\x24\x28' + struct.pack('<I', S_CLASS)
    # RegisterClassExA(&wc)
    e += b'\x54' + call_iat(VA_REGISTERCLASSEXA)
    # CreateWindowExA(0, S_CLASS, S_TITLE, WS_OL_VIS, 100,100,480,320, 0,0,0,0)
    e += push8(0) + push8(0) + push8(0) + push8(0)
    e += push32(320) + push32(480) + push32(100) + push32(100)
    e += push32(WS_OL_VIS) + push32(S_TITLE) + push32(S_CLASS) + push8(0)
    e += call_iat(VA_CREATEWINDOWEXA)
    # eax = hWnd
    e += b'\x89\xC6'          # mov esi, eax (save hWnd)
    e += push8(5) + b'\x56' + call_iat(VA_SHOWWINDOW)   # ShowWindow(hWnd,5)
    e += b'\x56' + call_iat(VA_UPDATEWINDOW)             # UpdateWindow(hWnd)
    # MSG loop: 28 bytes on stack
    e += sub_esp(28)
    loop = b'\x8D\x04\x24' + b'\x6A\x00'*3 + b'\x50' + call_iat(VA_GETMESSAGEA)  # lea eax,[esp]; push 0,0,0; push eax
    loop += b'\x85\xC0\x74\x14'   # test eax,eax; jz exit
    loop += b'\x54' + call_iat(VA_TRANSLATEMSG)
    loop += b'\x54' + call_iat(VA_DISPATCHMSG)
    loop += b'\xEB' + bytes([256 - len(loop) - 2])  # jmp back
    e += loop
    e += add_esp(28) + add_esp(48)
    e += push8(0) + call_iat(VA_EXITPROCESS) + int3()
    entry = bytes(e).ljust(0x110, b'\x90')

    # WndProc at +0xA0
    wp  = b'\x55\x8B\xEC'                    # push ebp; mov ebp, esp
    wp += b'\x8B\x45\x0C'                    # mov eax, [ebp+12] (uMsg)
    # WM_PAINT?
    wp += b'\x3D' + struct.pack('<I', WM_PAINT)
    wp += b'\x75\x30'   # jne not_paint (48 bytes)
    wp += sub_esp(64)    # PAINTSTRUCT
    wp += b'\x54\xFF\x75\x08' + call_iat(VA_BEGINPAINT)
    # eax=hdc; TextOutA(hdc, 20, 20, S_TEXT, len)
    wp += push32(len(b'Hello from Clawd!')) + push32(S_TEXT)
    wp += push32(20) + push32(20) + b'\x50'
    wp += call_iat(VA_TEXTOUTA)
    wp += b'\x54\xFF\x75\x08' + call_iat(VA_ENDPAINT)
    wp += add_esp(64) + xor_eax() + ret16()
    # WM_DESTROY?
    wp += b'\x3D' + struct.pack('<I', WM_DESTROY)
    wp += b'\x75\x0C'   # jne default
    wp += push8(0) + call_iat(VA_POSTQUITMSG)
    wp += xor_eax() + ret16()
    # default: DefWindowProcA
    wp += b'\xFF\x75\x14\xFF\x75\x10\xFF\x75\x0C\xFF\x75\x08'
    wp += call_iat(VA_DEFWINDOWPROCA) + ret16()

    return entry + bytes(wp), bytes(strs)

# ── TEST 3: FizzBuzz arithmetic test ─────────────────────────
def build_fizzbuzz():
    strs  = b'Clawd\x00'       # 0
    strs += b'FizzBuzz\x00'    # 6
    strs += b'Fizz\x00'        # 15
    strs += b'Buzz\x00'        # 20
    strs += b'FAIL\x00'        # 25
    S_CAP  = str_rva(0)
    S_FB   = str_rva(6)
    S_FIZZ = str_rva(15)
    S_BUZZ = str_rva(20)
    S_FAIL = str_rva(25)

    code = b''
    # 15 % 3 == 0?
    code += b'\xB8\x0F\x00\x00\x00\x99\xBB\x03\x00\x00\x00\xF7\xFB'  # mov eax,15; cdq; mov ebx,3; idiv ebx
    code += b'\x85\xD2\x75\x1A'  # test edx,edx; jnz not_fb
    # 15 % 5 == 0?
    code += b'\xB8\x0F\x00\x00\x00\x99\xBB\x05\x00\x00\x00\xF7\xFB'
    code += b'\x85\xD2\x75\x0C'  # jnz not_fb
    code += push8(0)+push32(S_CAP)+push32(S_FB)+push8(0)+call_iat(VA_MESSAGEBOXA)
    code += b'\xEB\x10'          # jmp done
    # not_fb:
    code += push8(0)+push32(S_CAP)+push32(S_FAIL)+push8(0)+call_iat(VA_MESSAGEBOXA)
    # done:
    code += push8(0)+call_iat(VA_EXITPROCESS)+int3()
    return code, bytes(strs)

# ── TEST 4: GDI drawing test ─────────────────────────────────
# Creates a window, on WM_PAINT: fills background blue, draws white text,
# draws a red rectangle border. Tests SetTextColor, FillRect, GetStockObject.
def build_gdi_draw():
    strs  = b'ClaWdDraw\x00'          # 0
    strs += b'Clawd GDI Draw Test\x00' # 10
    strs += b'GDI: SetTextColor + FillRect + TextOutA working!\x00'  # 30
    strs += b'Clawd\x00'              # 80
    S_CLASS = str_rva(0)
    S_TITLE = str_rva(10)
    S_TEXT  = str_rva(30)
    WM_PAINT = 0x000F
    WM_DESTROY = 0x0002
    WS_OL_VIS  = 0x00CF0000 | 0x10000000
    WNDPROC_VA = rva_to_va(TEXT_RVA + 0x110)

    # Entry: same pattern as build_window
    e = sub_esp(48)
    for off in range(0, 48, 4):
        e += b'\xC7\x44\x24' + bytes([off]) + b'\x00\x00\x00\x00'
    e += b'\xC7\x04\x24' + struct.pack('<I', 48)
    e += b'\xC7\x44\x24\x04' + struct.pack('<I', 3)
    e += b'\xC7\x44\x24\x08' + struct.pack('<I', WNDPROC_VA)
    e += b'\xC7\x44\x24\x28' + struct.pack('<I', S_CLASS)
    e += b'\x54' + call_iat(VA_REGISTERCLASSEXA)
    e += push8(0)+push8(0)+push8(0)+push8(0)
    e += push32(300)+push32(450)+push32(100)+push32(100)
    e += push32(WS_OL_VIS)+push32(S_TITLE)+push32(S_CLASS)+push8(0)
    e += call_iat(VA_CREATEWINDOWEXA)
    e += b'\x89\xC6'
    e += push8(5)+b'\x56'+call_iat(VA_SHOWWINDOW)
    e += b'\x56'+call_iat(VA_UPDATEWINDOW)
    e += sub_esp(28)
    loop = b'\x8D\x04\x24'+b'\x6A\x00'*3+b'\x50'+call_iat(VA_GETMESSAGEA)  # lea eax,[esp]; push 0,0,0; push eax; call
    loop += b'\x85\xC0\x74\x14'
    loop += b'\x54'+call_iat(VA_TRANSLATEMSG)
    loop += b'\x54'+call_iat(VA_DISPATCHMSG)
    loop += b'\xEB'+bytes([256-len(loop)-2])
    e += loop
    e += add_esp(28)+add_esp(48)
    e += push8(0)+call_iat(VA_EXITPROCESS)+int3()
    entry = bytes(e).ljust(0x110, b'\x90')

    # WndProc
    wp  = b'\x55\x8B\xEC'
    wp += b'\x8B\x45\x0C'
    # WM_PAINT
    wp += b'\x3D' + struct.pack('<I', WM_PAINT)
    wp += b'\x75\x50'  # jne ~80 bytes
    wp += sub_esp(64)  # PAINTSTRUCT
    wp += b'\x54\xFF\x75\x08'+call_iat(VA_BEGINPAINT)
    wp += b'\x89\xC7'  # mov edi, eax (hdc)
    # FillRect(hdc, NULL /*full*/, CreateSolidBrush(0x00FF0000=blue BGR))
    wp += push32(0x00FF0000)+call_iat(VA_CREATESOLIDBR)  # blue brush
    wp += push32(0)   # lprc = NULL (whole client)
    wp += b'\x50'     # push eax (hBrush)
    wp += b'\x57'     # push edi (hdc)
    wp += call_iat(VA_FILLRECT)
    # SetBkMode(hdc, TRANSPARENT=1)
    wp += push8(1)+b'\x57'+call_iat(VA_SETBKMODE)
    # SetTextColor(hdc, 0x00FFFFFF white)
    wp += push32(0x00FFFFFF)+b'\x57'+call_iat(VA_SETTEXTCOLOR)
    # TextOutA(hdc, 10, 10, S_TEXT, len)
    txt = b'GDI: SetTextColor + FillRect + TextOutA working!'
    wp += push32(len(txt))+push32(S_TEXT)+push32(10)+push32(10)+b'\x57'
    wp += call_iat(VA_TEXTOUTA)
    wp += b'\x54\xFF\x75\x08'+call_iat(VA_ENDPAINT)
    wp += add_esp(64)+xor_eax()+ret16()
    # WM_DESTROY
    wp += b'\x3D'+struct.pack('<I', WM_DESTROY)+b'\x75\x0C'
    wp += push8(0)+call_iat(VA_POSTQUITMSG)+xor_eax()+ret16()
    # default
    wp += b'\xFF\x75\x14\xFF\x75\x10\xFF\x75\x0C\xFF\x75\x08'
    wp += call_iat(VA_DEFWINDOWPROCA)+ret16()

    return entry+bytes(wp), bytes(strs)

# Build all
tests = {
    'clawd_msgbox_loop.exe': build_msgbox_loop(),
    'clawd_window.exe':      build_window(),
    'clawd_fizzbuzz.exe':    build_fizzbuzz(),
    'clawd_gdi_draw.exe':    build_gdi_draw(),
}
for name, (code, strs) in tests.items():
    pe = build_pe(code, IMPORTS, strs)
    with open(OUT + name, "wb") as f:
        f.write(pe)
    print(f"Built {name}: {len(pe)} bytes")
print("Done!")
