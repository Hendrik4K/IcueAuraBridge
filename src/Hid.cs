// Small HID access layer (SetupAPI + hid.dll), enough to talk to vendor-defined
// 65-byte report devices such as the ASUS AURA LED controller.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace IcueAuraBridge
{
    internal sealed class HidDeviceInfo
    {
        public string Path;
        public ushort VendorId;
        public ushort ProductId;
        public ushort UsagePage;
        public ushort Usage;
        public int OutputReportLength;
        public int InputReportLength;
    }

    internal sealed class HidDevice : IDisposable
    {
        readonly FileStream stream;
        readonly int outLen;
        readonly int inLen;

        HidDevice(FileStream stream, int outLen, int inLen)
        {
            this.stream = stream;
            this.outLen = outLen;
            this.inLen = inLen;
        }

        public static List<HidDeviceInfo> Enumerate(ushort vendorId)
        {
            var result = new List<HidDeviceInfo>();
            Guid hidGuid;
            Native.HidD_GetHidGuid(out hidGuid);
            IntPtr set = Native.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1))
                return result;
            try
            {
                for (uint i = 0; ; i++)
                {
                    var data = new Native.SP_DEVICE_INTERFACE_DATA();
                    data.cbSize = Marshal.SizeOf(data);
                    if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref data))
                        break;
                    string path = GetPath(set, ref data);
                    if (path == null || path.IndexOf("vid_" + vendorId.ToString("x4"), StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    var info = Query(path);
                    if (info != null)
                        result.Add(info);
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(set);
            }
            return result;
        }

        static string GetPath(IntPtr set, ref Native.SP_DEVICE_INTERFACE_DATA data)
        {
            uint size;
            Native.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out size, IntPtr.Zero);
            if (size == 0)
                return null;
            IntPtr buf = Marshal.AllocHGlobal((int)size);
            try
            {
                // cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W: 8 on x64, 6 on x86
                Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref data, buf, size, out size, IntPtr.Zero))
                    return null;
                return Marshal.PtrToStringUni(buf + 4);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        static HidDeviceInfo Query(string path)
        {
            // Access 0 is enough for attributes/caps and works even if another process holds the device.
            using (var h = Native.CreateFile(path, 0, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero))
            {
                if (h.IsInvalid)
                    return null;
                var attr = new Native.HIDD_ATTRIBUTES();
                attr.Size = Marshal.SizeOf(attr);
                if (!Native.HidD_GetAttributes(h, ref attr))
                    return null;
                IntPtr pre;
                if (!Native.HidD_GetPreparsedData(h, out pre))
                    return null;
                var caps = new Native.HIDP_CAPS();
                try
                {
                    Native.HidP_GetCaps(pre, out caps);
                }
                finally
                {
                    Native.HidD_FreePreparsedData(pre);
                }
                var info = new HidDeviceInfo();
                info.Path = path;
                info.VendorId = attr.VendorID;
                info.ProductId = attr.ProductID;
                info.UsagePage = caps.UsagePage;
                info.Usage = caps.Usage;
                info.OutputReportLength = caps.OutputReportByteLength;
                info.InputReportLength = caps.InputReportByteLength;
                return info;
            }
        }

        public static HidDevice Open(HidDeviceInfo info)
        {
            var h = Native.CreateFile(info.Path, Native.GENERIC_READ | Native.GENERIC_WRITE, FileShare.ReadWrite,
                                      IntPtr.Zero, FileMode.Open, Native.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (h.IsInvalid)
                throw new IOException("Cannot open HID device (error " + Marshal.GetLastWin32Error() + ")");
            var fs = new FileStream(h, FileAccess.ReadWrite, 4096, true);
            return new HidDevice(fs, info.OutputReportLength, info.InputReportLength);
        }

        // Writes one output report; data starts with the report id and is zero-padded.
        public void Write(byte[] data)
        {
            var buf = new byte[outLen];
            Array.Copy(data, buf, Math.Min(data.Length, outLen));
            var t = stream.WriteAsync(buf, 0, buf.Length);
            if (!t.Wait(1000))
                throw new IOException("HID write timed out");
        }

        // Reads one input report, or returns null on timeout.
        public byte[] Read(int timeoutMs)
        {
            var buf = new byte[inLen];
            Task<int> t = stream.ReadAsync(buf, 0, buf.Length);
            if (!t.Wait(timeoutMs))
                return null;   // the pending read is cancelled when the stream is closed
            return t.Result > 0 ? buf : null;
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        static class Native
        {
            public const uint DIGCF_PRESENT = 0x02;
            public const uint DIGCF_DEVICEINTERFACE = 0x10;
            public const uint GENERIC_READ = 0x80000000;
            public const uint GENERIC_WRITE = 0x40000000;
            public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

            [StructLayout(LayoutKind.Sequential)]
            public struct SP_DEVICE_INTERFACE_DATA
            {
                public int cbSize;
                public Guid InterfaceClassGuid;
                public int Flags;
                public IntPtr Reserved;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct HIDD_ATTRIBUTES
            {
                public int Size;
                public ushort VendorID;
                public ushort ProductID;
                public ushort VersionNumber;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct HIDP_CAPS
            {
                public ushort Usage;
                public ushort UsagePage;
                public ushort InputReportByteLength;
                public ushort OutputReportByteLength;
                public ushort FeatureReportByteLength;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
                public ushort[] Reserved;
                public ushort NumberLinkCollectionNodes;
                public ushort NumberInputButtonCaps;
                public ushort NumberInputValueCaps;
                public ushort NumberInputDataIndices;
                public ushort NumberOutputButtonCaps;
                public ushort NumberOutputValueCaps;
                public ushort NumberOutputDataIndices;
                public ushort NumberFeatureButtonCaps;
                public ushort NumberFeatureValueCaps;
                public ushort NumberFeatureDataIndices;
            }

            [DllImport("hid.dll")]
            public static extern void HidD_GetHidGuid(out Guid guid);

            [DllImport("hid.dll", SetLastError = true)]
            public static extern bool HidD_GetAttributes(SafeFileHandle device, ref HIDD_ATTRIBUTES attributes);

            [DllImport("hid.dll", SetLastError = true)]
            public static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsed);

            [DllImport("hid.dll")]
            public static extern bool HidD_FreePreparsedData(IntPtr preparsed);

            [DllImport("hid.dll")]
            public static extern int HidP_GetCaps(IntPtr preparsed, out HIDP_CAPS caps);

            [DllImport("setupapi.dll", CharSet = CharSet.Unicode, EntryPoint = "SetupDiGetClassDevsW")]
            public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

            [DllImport("setupapi.dll", SetLastError = true)]
            public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid guid, uint index, ref SP_DEVICE_INTERFACE_DATA data);

            [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetDeviceInterfaceDetailW")]
            public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, uint detailSize, out uint requiredSize, IntPtr devInfo);

            [DllImport("setupapi.dll")]
            public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
            public static extern SafeFileHandle CreateFile(string name, uint access, FileShare share, IntPtr security, FileMode mode, uint flags, IntPtr template);
        }
    }
}
