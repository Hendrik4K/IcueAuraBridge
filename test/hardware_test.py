"""Drives the real hardware through the bridge exactly like iCUE's asus_plugin.dll:
CoCreateInstance(CLSID_AuraSdk, CLSCTX_INPROC_SERVER, IID_IAuraSdk2) and raw vtable calls.
Shows red, green, blue, then a 4 s rainbow, then white. The DLL must be registered.
"""
import ctypes, colorsys, time, uuid
from ctypes import POINTER, byref, c_void_p, c_uint32, c_int32, c_ubyte

ole32 = ctypes.OleDLL('ole32')
oleaut32 = ctypes.WinDLL('oleaut32')
oleaut32.SysFreeString.argtypes = [c_void_p]

class GUID(ctypes.Structure):
    _fields_ = [('b', ctypes.c_ubyte * 16)]
    def __init__(self, s):
        super().__init__()
        ctypes.memmove(self.b, uuid.UUID(s).bytes_le, 16)

def slot(ptr, offset, *argtypes):
    fn = ctypes.cast(ctypes.cast(ptr, POINTER(c_void_p))[0], POINTER(c_void_p))[offset // 8]
    f = ctypes.WINFUNCTYPE(ctypes.HRESULT, c_void_p, *argtypes)(fn)
    return lambda *a: f(ptr, *a)

def bstr(ptr, off):
    out = c_void_p()
    slot(ptr, off, POINTER(c_void_p))(byref(out))
    v = ctypes.wstring_at(out.value)
    oleaut32.SysFreeString(out)
    return v

ole32.CoInitializeEx(None, 0)
sdk = c_void_p()
ole32.CoCreateInstance(byref(GUID('05921124-5057-483E-A037-E9497B523590')), None, 1,
                       byref(GUID('EE69DBAE-33FF-4E45-B378-01797A59852D')), byref(sdk))
slot(sdk, 0x40)()  # SwitchMode
coll = c_void_p()
t = time.time()
slot(sdk, 0x38, c_uint32, POINTER(c_void_p))(0, byref(coll))
cnt = c_int32(); slot(coll, 0x40, POINTER(c_int32))(byref(cnt))
print('Enumerate: %d device(s) in %.2fs' % (cnt.value, time.time() - t))

devices = []
for i in range(cnt.value):
    dev = c_void_p(); slot(coll, 0x48, c_int32, POINTER(c_void_p))(i, byref(dev))
    typ = c_uint32(); slot(dev, 0x40, POINTER(c_uint32))(byref(typ))
    lights = c_void_p(); slot(dev, 0x38, POINTER(c_void_p))(byref(lights))
    lc = c_int32(); slot(lights, 0x40, POINTER(c_int32))(byref(lc))
    ls = []
    for j in range(lc.value):
        l = c_void_p(); slot(lights, 0x48, c_int32, POINTER(c_void_p))(j, byref(l)); ls.append(l)
    print('  0x%08X %r: %s' % (typ.value, bstr(dev, 0x48), [bstr(l, 0x68) for l in ls]))
    devices.append((dev, ls))

def frame(fn):
    for dev, ls in devices:
        for j, l in enumerate(ls):
            r, g, b = fn(j, len(ls))
            slot(l, 0x40, c_ubyte)(r); slot(l, 0x50, c_ubyte)(g); slot(l, 0x60, c_ubyte)(b)
        slot(dev, 0x60)()  # Apply

for name, rgb in (('red', (255, 0, 0)), ('green', (0, 255, 0)), ('blue', (0, 0, 255))):
    print(name); frame(lambda j, n: rgb); time.sleep(1.5)

print('rainbow 4 s @ 30 fps')
t0 = time.time()
while time.time() - t0 < 4:
    ph = time.time() - t0
    frame(lambda j, n: tuple(int(255 * x) for x in colorsys.hsv_to_rgb((ph / 2 + j / max(n, 1)) % 1, 1, 1)))
    time.sleep(1 / 30)
print('white'); frame(lambda j, n: (255, 255, 255)); time.sleep(1.5)
