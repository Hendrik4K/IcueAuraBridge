"""Read-only hardware probe: ASUS Aura USB config table and NVIDIA I2C RGB controllers.

Mainboard: sends EC 82 (firmware) and EC B0 (config table) to every 0B05 HID interface.
GPU: via NVAPI I2C on port 1, reads Aura-GPU magic (regs 0x20/0x21 at 0x29/0x2A) and
checks whether something answers at the ENE address 0x67. Nothing is written to any
LED register.
"""
import ctypes, struct
from ctypes import wintypes, byref, c_void_p, c_uint32, c_int32, c_uint8, POINTER

# ---------------- HID ----------------
hid = ctypes.WinDLL('hid')
setupapi = ctypes.WinDLL('setupapi')
k32 = ctypes.WinDLL('kernel32', use_last_error=True)

class GUID(ctypes.Structure):
    _fields_ = [('d1', c_uint32), ('d2', ctypes.c_uint16), ('d3', ctypes.c_uint16), ('d4', c_uint8 * 8)]

class SP_DEVICE_INTERFACE_DATA(ctypes.Structure):
    _fields_ = [('cbSize', c_uint32), ('guid', GUID), ('flags', c_uint32), ('reserved', ctypes.c_size_t)]

class HIDD_ATTRIBUTES(ctypes.Structure):
    _fields_ = [('Size', c_uint32), ('VendorID', ctypes.c_uint16), ('ProductID', ctypes.c_uint16), ('Version', ctypes.c_uint16)]

class HIDP_CAPS(ctypes.Structure):
    _fields_ = [('Usage', ctypes.c_uint16), ('UsagePage', ctypes.c_uint16), ('InLen', ctypes.c_uint16),
                ('OutLen', ctypes.c_uint16), ('FeatLen', ctypes.c_uint16), ('Reserved', ctypes.c_uint16 * 17),
                ('rest', ctypes.c_uint16 * 10)]

setupapi.SetupDiGetClassDevsW.restype = c_void_p
setupapi.SetupDiGetClassDevsW.argtypes = [POINTER(GUID), c_void_p, c_void_p, c_uint32]
setupapi.SetupDiEnumDeviceInterfaces.argtypes = [c_void_p, c_void_p, POINTER(GUID), c_uint32, POINTER(SP_DEVICE_INTERFACE_DATA)]
setupapi.SetupDiGetDeviceInterfaceDetailW.argtypes = [c_void_p, POINTER(SP_DEVICE_INTERFACE_DATA), c_void_p, c_uint32, POINTER(c_uint32), c_void_p]
k32.CreateFileW.restype = c_void_p
k32.CreateFileW.argtypes = [wintypes.LPCWSTR, c_uint32, c_uint32, c_void_p, c_uint32, c_uint32, c_void_p]
k32.CloseHandle.argtypes = [c_void_p]
k32.WriteFile.argtypes = [c_void_p, c_void_p, c_uint32, POINTER(c_uint32), c_void_p]
k32.ReadFile.argtypes = [c_void_p, c_void_p, c_uint32, POINTER(c_uint32), c_void_p]
k32.CancelIoEx.argtypes = [c_void_p, c_void_p]
k32.GetOverlappedResult.argtypes = [c_void_p, c_void_p, POINTER(c_uint32), c_uint32]
k32.CreateEventW.restype = c_void_p
k32.WaitForSingleObject.argtypes = [c_void_p, c_uint32]
hid.HidD_GetAttributes.argtypes = [c_void_p, POINTER(HIDD_ATTRIBUTES)]
hid.HidD_GetPreparsedData.argtypes = [c_void_p, POINTER(c_void_p)]
hid.HidP_GetCaps.argtypes = [c_void_p, POINTER(HIDP_CAPS)]
hid.HidD_FreePreparsedData.argtypes = [c_void_p]

class OVERLAPPED(ctypes.Structure):
    _fields_ = [('Internal', ctypes.c_size_t), ('InternalHigh', ctypes.c_size_t), ('Offset', c_uint32),
                ('OffsetHigh', c_uint32), ('hEvent', c_void_p)]

def hid_paths():
    g = GUID()
    hid.HidD_GetHidGuid(byref(g))
    h = setupapi.SetupDiGetClassDevsW(byref(g), None, None, 0x12)
    i = 0
    while True:
        d = SP_DEVICE_INTERFACE_DATA(); d.cbSize = ctypes.sizeof(d)
        if not setupapi.SetupDiEnumDeviceInterfaces(h, None, byref(g), i, byref(d)):
            break
        need = c_uint32()
        setupapi.SetupDiGetDeviceInterfaceDetailW(h, byref(d), None, 0, byref(need), None)
        buf = ctypes.create_string_buffer(need.value)
        struct.pack_into('<I', buf, 0, 8)
        setupapi.SetupDiGetDeviceInterfaceDetailW(h, byref(d), buf, need.value, None, None)
        yield ctypes.wstring_at(ctypes.addressof(buf) + 4)
        i += 1

def transact(handle, payload, outlen):
    pkt = bytes([0xEC]) + payload
    pkt = pkt + b'\0' * (outlen - len(pkt))
    w = c_uint32()
    ov = OVERLAPPED(); ov.hEvent = k32.CreateEventW(None, 1, 0, None)
    k32.WriteFile(handle, pkt, len(pkt), byref(w), byref(ov))
    k32.WaitForSingleObject(ov.hEvent, 1000)
    rb = ctypes.create_string_buffer(outlen)
    ov2 = OVERLAPPED(); ov2.hEvent = k32.CreateEventW(None, 1, 0, None)
    k32.ReadFile(handle, rb, outlen, byref(w), byref(ov2))
    if k32.WaitForSingleObject(ov2.hEvent, 1000) != 0:
        k32.CancelIoEx(handle, byref(ov2))
        return None
    k32.GetOverlappedResult(handle, byref(ov2), byref(w), 0)
    return rb.raw[:w.value]

print('== ASUS HID interfaces')
for p in hid_paths():
    if 'vid_0b05' not in p.lower():
        continue
    h = k32.CreateFileW(p, 0xC0000000, 3, None, 3, 0x40000000, None)  # overlapped
    if h in (None, c_void_p(-1).value):
        print(p, 'open failed', ctypes.get_last_error()); continue
    a = HIDD_ATTRIBUTES(); a.Size = ctypes.sizeof(a); hid.HidD_GetAttributes(h, byref(a))
    pp = c_void_p(); hid.HidD_GetPreparsedData(h, byref(pp)); caps = HIDP_CAPS(); hid.HidP_GetCaps(pp, byref(caps)); hid.HidD_FreePreparsedData(pp)
    print('%s\n  pid=%04X page=%04X usage=%04X in=%d out=%d' % (p, a.ProductID, caps.UsagePage, caps.Usage, caps.InLen, caps.OutLen))
    if caps.OutLen >= 65:
        fw = transact(h, b'\x82', caps.OutLen)
        print('  firmware reply:', fw[:20].hex(' ') if fw else None, repr(fw[2:18]) if fw else '')
        cfg = transact(h, b'\xB0', caps.OutLen)
        if cfg:
            print('  config reply  :', cfg.hex(' '))
            if cfg[1] == 0x30:
                t = cfg[4:]
                print('  -> addressable headers=%d, mainboard LEDs total=%d, 12V RGB headers=%d' % (t[0x02], t[0x1B], t[0x1D]))
    k32.CloseHandle(h)

# ---------------- NVAPI I2C ----------------
print('== NVAPI')
nv = ctypes.CDLL('nvapi64')
nv.nvapi_QueryInterface.restype = c_void_p
nv.nvapi_QueryInterface.argtypes = [c_uint32]
def fn(id_, *args):
    return ctypes.CFUNCTYPE(c_int32, *args)(nv.nvapi_QueryInterface(id_))

Initialize = fn(0x0150E828)
EnumGPUs = fn(0xE5AC921F, POINTER(c_void_p), POINTER(c_int32))
GetPCI = fn(0x2DDFB66E, c_void_p, POINTER(c_uint32), POINTER(c_uint32), POINTER(c_uint32), POINTER(c_uint32))

class NV_I2C_INFO_V3(ctypes.Structure):
    _fields_ = [('version', c_uint32), ('display_mask', c_uint32), ('is_ddc_port', c_uint8), ('i2c_dev_address', c_uint8),
                ('i2c_reg_address', POINTER(c_uint8)), ('reg_addr_size', c_uint32), ('data', POINTER(c_uint8)),
                ('size', c_uint32), ('i2c_speed', c_uint32), ('i2c_speed_khz', c_uint32), ('port_id', c_uint8),
                ('is_port_id_set', c_uint32)]

ReadEx = fn(0x4D7B0709, c_void_p, POINTER(NV_I2C_INFO_V3), POINTER(c_uint32))
WriteEx = fn(0x283AC65A, c_void_p, POINTER(NV_I2C_INFO_V3), POINTER(c_uint32))
print('sizeof NV_I2C_INFO_V3 =', ctypes.sizeof(NV_I2C_INFO_V3))
print('Initialize ->', Initialize())
handles = (c_void_p * 64)(); n = c_int32()
print('EnumPhysicalGPUs ->', EnumGPUs(handles, byref(n)), 'count', n.value)

def xfer(gpu, addr7, reg, data, write):
    """SMBus-style transfer via NVAPI. reg=None -> no register byte."""
    i = NV_I2C_INFO_V3()
    i.version = (3 << 16) | ctypes.sizeof(NV_I2C_INFO_V3)
    i.i2c_dev_address = addr7 << 1
    regbuf = (c_uint8 * 1)(reg if reg is not None else 0)
    i.i2c_reg_address = ctypes.cast(regbuf, POINTER(c_uint8)) if reg is not None else None
    i.reg_addr_size = 1 if reg is not None else 0
    i.data = ctypes.cast(data, POINTER(c_uint8))
    i.size = len(data)
    i.i2c_speed = 0xFFFF
    i.port_id = 1
    i.is_port_id_set = 1
    unk = c_uint32(0)
    return (WriteEx if write else ReadEx)(gpu, byref(i), byref(unk))

def read_byte_data(gpu, addr, cmd):
    d = (c_uint8 * 1)()
    st = xfer(gpu, addr, cmd, d, False)
    return d[0] if st == 0 else -1

def ene_read(gpu, addr, reg):
    w = (c_uint8 * 2)(reg >> 8, reg & 0xFF)   # word ((reg<<8)|(reg>>8)) sent low byte first
    if xfer(gpu, addr, 0x00, w, True) != 0:
        return -1
    return read_byte_data(gpu, addr, 0x81)

for g in range(n.value):
    gpu = handles[g]
    dev, sub, rev, ext = c_uint32(), c_uint32(), c_uint32(), c_uint32()
    GetPCI(gpu, byref(dev), byref(sub), byref(rev), byref(ext))
    print('GPU %d: %04X:%04X subsys %04X:%04X' % (g, dev.value & 0xFFFF, dev.value >> 16, sub.value & 0xFFFF, sub.value >> 16))
    sig = [read_byte_data(gpu, 0x67, c) for c in range(0xA0, 0xB0)]
    print('  ENE @0x67 signature A0..AF:', sig, 'OK' if sig == list(range(16)) else 'MISMATCH')
    if sig == list(range(16)):
        name = bytes(max(0, ene_read(gpu, 0x67, 0x1000 + k)) for k in range(16))
        print('  device name:', name)
        cfg = [ene_read(gpu, 0x67, 0x1C00 + k) for k in range(64)]
        print('  config table:', ' '.join('%02X' % (v & 0xFF) for v in cfg))
        print('  led_count[0x02]=%d [0x03]=%d  ch_v1[0x13..]=%s ch_v2[0x1B..]=%s' % (
            cfg[2], cfg[3], ' '.join('%02X' % v for v in cfg[0x13:0x1B]), ' '.join('%02X' % v for v in cfg[0x1B:0x23])))
        print('  current: direct(0x8020)=%d mode(0x8021)=%d' % (ene_read(gpu, 0x67, 0x8020), ene_read(gpu, 0x67, 0x8021)))
