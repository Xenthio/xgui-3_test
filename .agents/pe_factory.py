#!/usr/bin/env python3
"""Minimal Win32 PE factory for test executables."""
import struct, os

IMAGE_BASE = 0x00400000
TEXT_RVA   = 0x1000
RDATA_RVA  = 0x2000
TEXT_RAW   = 0x0200
RDATA_RAW  = 0x1200

def rva_to_va(rva): return IMAGE_BASE + rva
def str_rva(user_offset): return rva_to_va(RDATA_RVA + 0x800 + user_offset)

def build_pe(code_bytes, imports, strings_data):
    rdata = bytearray(0x1000)
    dll_names = sorted(imports.keys())
    n_dlls = len(dll_names)
    idt_off = 0x000
    ilt_off = 0x100
    iat_off = 0x300
    dllname_off = 0x400
    hint_off    = 0x500
    user_off    = 0x800
    rdata[user_off:user_off+len(strings_data)] = strings_data
    hint_cursor = hint_off
    dllname_cursor = dllname_off
    ilt_cursor = ilt_off
    iat_cursor = iat_off
    dll_meta = []
    for dll in dll_names:
        funcs = imports[dll]
        name_bytes = dll.upper().encode('ascii') + b'\x00'
        dll_name_rva = RDATA_RVA + dllname_cursor
        rdata[dllname_cursor:dllname_cursor+len(name_bytes)] = name_bytes
        dllname_cursor += (len(name_bytes) + 1) & ~1
        cur_ilt_rva = RDATA_RVA + ilt_cursor
        cur_iat_rva = RDATA_RVA + iat_cursor
        for func_name, iat_va in funcs:
            hint_rva = RDATA_RVA + hint_cursor
            rdata[hint_cursor:hint_cursor+2] = b'\x00\x00'
            fn_bytes = func_name.encode('ascii') + b'\x00'
            rdata[hint_cursor+2:hint_cursor+2+len(fn_bytes)] = fn_bytes
            hint_cursor += (2 + len(fn_bytes) + 1) & ~1
            struct.pack_into('<I', rdata, ilt_cursor, hint_rva)
            struct.pack_into('<I', rdata, iat_cursor, hint_rva)
            ilt_cursor += 4
            iat_cursor += 4
        struct.pack_into('<I', rdata, ilt_cursor, 0)
        struct.pack_into('<I', rdata, iat_cursor, 0)
        ilt_cursor += 4
        iat_cursor += 4
        dll_meta.append((dll_name_rva, cur_ilt_rva, cur_iat_rva, funcs))
    for i, (dll_name_rva, cur_ilt_rva, cur_iat_rva, funcs) in enumerate(dll_meta):
        off = idt_off + i * 20
        struct.pack_into('<IIIII', rdata, off, cur_ilt_rva, 0, 0, dll_name_rva, cur_iat_rva)
    struct.pack_into('<IIIII', rdata, idt_off + n_dlls * 20, 0,0,0,0,0)
    import_dir_rva  = RDATA_RVA + idt_off
    import_dir_size = (n_dlls + 1) * 20
    iat_data_rva    = RDATA_RVA + iat_off
    iat_data_size   = iat_cursor - iat_off
    mz = b'MZ' + b'\x00'*0x3A + struct.pack('<I', 0x80)
    mz = mz.ljust(0x80, b'\x00')
    opt = struct.pack('<HBB', 0x010B, 8, 0)
    opt += struct.pack('<III', len(code_bytes), 0, len(rdata))
    opt += struct.pack('<I', TEXT_RVA)
    opt += struct.pack('<II', TEXT_RVA, RDATA_RVA)
    opt += struct.pack('<I', IMAGE_BASE)
    opt += struct.pack('<II', 0x1000, 0x0200)
    opt += struct.pack('<HH', 5, 0)
    opt += struct.pack('<HH', 0, 0)
    opt += struct.pack('<HH', 4, 0)
    opt += struct.pack('<I', 0)
    opt += struct.pack('<I', 0x5000)
    opt += struct.pack('<I', 0x0200)
    opt += struct.pack('<I', 0)
    opt += struct.pack('<H', 2)
    opt += struct.pack('<H', 0)
    opt += struct.pack('<II', 0x100000, 0x1000)
    opt += struct.pack('<II', 0x100000, 0x1000)
    opt += struct.pack('<I', 0)
    opt += struct.pack('<I', 16)
    data_dirs = bytearray(16*8)
    struct.pack_into('<II', data_dirs, 1*8,  import_dir_rva,  import_dir_size)
    struct.pack_into('<II', data_dirs, 12*8, iat_data_rva, iat_data_size)
    opt += bytes(data_dirs)
    assert len(opt) == 0xE0, f"opt={len(opt)}"
    coff = struct.pack('<HHIIIH', 0x014C, 2, 0, 0, 0, len(opt))
    coff += struct.pack('<H', 0x0002)
    def sec(name, vaddr, raw_off, chars):
        n = (name.encode('ascii') if isinstance(name, str) else name).ljust(8,b'\x00')[:8]
        return struct.pack('<8sIIIIIIHHI', n, 0x1000, vaddr, 0x1000, raw_off, 0,0,0,0, chars)
    headers = mz + b'PE\x00\x00' + coff + opt
    headers += sec(b'.text',  TEXT_RVA,  TEXT_RAW,  0x60000020)
    headers += sec(b'.rdata', RDATA_RVA, RDATA_RAW, 0x40000040)
    headers = headers.ljust(0x0200, b'\x00')
    return headers + bytes(code_bytes).ljust(0x1000,b'\x00') + bytes(rdata).ljust(0x1000,b'\x00')

# Helpers
def call_iat(va):   return b'\xFF\x15' + struct.pack('<I', va)
def push32(v):      return b'\x68' + struct.pack('<I', v)
def push8(v):       return bytes([0x6A, v & 0xFF])
def ret16():        return b'\x5D\xC2\x10\x00'   # pop ebp; ret 16
def xor_eax():      return b'\x33\xC0'
def sub_esp(n):     return bytes([0x83,0xEC,n]) if n<=127 else b'\x81\xEC'+struct.pack('<I',n)
def add_esp(n):     return bytes([0x83,0xC4,n]) if n<=127 else b'\x81\xC4'+struct.pack('<I',n)
def int3():         return b'\xCC'

print("pe_factory loaded")

# ── Dynamic IAT VA resolver ────────────────────────────────────────────────
# Given an ordered list of (dll, func) pairs (in the order they'll appear in the
# IMPORTS dict, noting build_pe sorts by dll name), returns a dict mapping
# func_name -> IAT VA.
def resolve_iat_vas(imports_dict):
    """Compute the IAT VA for every function in imports_dict.
    imports_dict format: {dll: [(func,_), ...], ...}
    DLLs are sorted alphabetically (same as build_pe).
    Returns: {func_name: VA, ...}
    """
    dll_names = sorted(imports_dict.keys())
    slot = 0
    result = {}
    for dll in dll_names:
        for func_name, _ in imports_dict[dll]:
            result[func_name] = IMAGE_BASE + RDATA_RVA + 0x300 + slot * 4
            slot += 1
        slot += 1  # null terminator between DLLs
    return result
