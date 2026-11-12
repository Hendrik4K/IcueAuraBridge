// Binary-compatible re-implementation of the ASUS AuraServiceLib COM interfaces
// (Aura SDK v3.1) as used by iCUE's plugins\ASUS\asus_plugin.dll.
//
// The IIDs come from ASUS' public SDK documentation. The vtable order was verified
// against the call sites in asus_plugin.dll 1.2.4 (all interfaces are dual, so the
// first own slot is at offset 0x38, right after IUnknown + IDispatch):
//
//   IAuraSdk2               0x38 Enumerate  0x40 SwitchMode  0x48 ReleaseControl
//   I*Collection            0x38 _NewEnum   0x40 Count       0x48 Item
//   IAuraSyncDevice         0x38 Lights     0x40 Type        0x48 Name
//                           0x50 Width      0x58 Height      0x60 Apply
//   IAuraRgbLight           0x38/0x40 Red   0x48/0x50 Green  0x58/0x60 Blue
//                           0x68 Name       0x70/0x78 Color
//
// .NET does not lay out inherited COM interfaces, so IAuraSdk2 repeats the members
// of IAuraSdk. Members are declared as plain methods (not properties) so the slot
// order is exactly the declaration order.

using System.Runtime.InteropServices;

namespace IcueAuraBridge
{
    public static class AuraIds
    {
        public const string LibId = "F1AA5209-5217-4B82-BA7E-A68198999AFA";
        public const string ClsidAuraSdk = "05921124-5057-483E-A037-E9497B523590";
        public const string ProgId = "aura.sdk.1";
    }

    public static class AuraDeviceType
    {
        public const uint All = 0x00000000;
        public const uint Motherboard = 0x00010000;
        public const uint MotherboardLedStrip = 0x00011000;
        public const uint Gpu = 0x00020000;
    }

    [ComVisible(true)]
    [Guid("3CED2297-27BD-492C-9934-D1D153B0FAC1")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IAuraSdk
    {
        [DispId(1)] IAuraSyncDeviceCollection Enumerate(uint devType);
        [DispId(2)] void SwitchMode();
    }

    [ComVisible(true)]
    [Guid("EE69DBAE-33FF-4E45-B378-01797A59852D")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IAuraSdk2
    {
        [DispId(1)] IAuraSyncDeviceCollection Enumerate(uint devType);
        [DispId(2)] void SwitchMode();
        [DispId(3)] void ReleaseControl(uint reserve);
    }

    [ComVisible(true)]
    [Guid("87FC56AB-99CA-4FD3-B561-2EEDD719DA57")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IAuraSyncDeviceCollection
    {
        [DispId(-4)]
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object get__NewEnum();
        [DispId(1)] int get_Count();
        [DispId(0)] IAuraSyncDevice get_Item(int index);
    }

    [ComVisible(true)]
    [Guid("6A30D789-F5DA-4F26-BF09-6DFB9DEDF91E")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IAuraSyncDevice
    {
        [DispId(1)] IAuraRgbLightCollection get_Lights();
        [DispId(2)] uint get_Type();
        [DispId(3)] [return: MarshalAs(UnmanagedType.BStr)] string get_Name();
        [DispId(4)] uint get_Width();
        [DispId(5)] uint get_Height();
        [DispId(6)] void Apply();
    }

    [ComVisible(true)]
    [Guid("003065BC-B562-454E-ADCB-A9B70042D486")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IAuraRgbLightCollection
    {
        [DispId(-4)]
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object get__NewEnum();
        [DispId(1)] int get_Count();
        [DispId(0)] IAuraRgbLight get_Item(int index);
    }

    [ComVisible(true)]
    [Guid("9AF6260E-4311-417D-B3EF-B85A34CF3244")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IAuraRgbLight
    {
        [DispId(1)] byte get_Red();
        [DispId(2)] void put_Red(byte value);
        [DispId(3)] byte get_Green();
        [DispId(4)] void put_Green(byte value);
        [DispId(5)] byte get_Blue();
        [DispId(6)] void put_Blue(byte value);
        [DispId(7)] [return: MarshalAs(UnmanagedType.BStr)] string get_Name();
        [DispId(8)] uint get_Color();
        [DispId(9)] void put_Color(uint value);
    }
}
