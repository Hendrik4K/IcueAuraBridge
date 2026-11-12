// SMBus-style I2C access to the RGB controller on NVIDIA cards through NVAPI
// (nvapi64.dll ships with the NVIDIA driver). Function ids and the NV_I2C_INFO_V3
// layout are the ones used by OpenRGB's i2c_smbus_nvapi; RGB controllers sit on port 1.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IcueAuraBridge
{
    internal sealed class NvGpu
    {
        public IntPtr Handle;
        public ushort DeviceId;
        public ushort SubsystemVendorId;
        public ushort SubsystemDeviceId;
        public string Name;
    }

    internal static class NvApiI2c
    {
        [StructLayout(LayoutKind.Sequential)]
        struct NV_I2C_INFO_V3
        {
            public uint version;
            public uint display_mask;
            public byte is_ddc_port;
            public byte i2c_dev_address;
            public IntPtr i2c_reg_address;
            public uint reg_addr_size;
            public IntPtr data;
            public uint size;
            public uint i2c_speed;
            public uint i2c_speed_khz;
            public byte port_id;
            public uint is_port_id_set;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int InitializeFn();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int EnumPhysicalGpusFn([Out] IntPtr[] handles, out int count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GetPciIdentifiersFn(IntPtr gpu, out uint deviceId, out uint subSystemId, out uint revisionId, out uint extDeviceId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GetFullNameFn(IntPtr gpu, [Out] byte[] name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int I2cFn(IntPtr gpu, ref NV_I2C_INFO_V3 info, ref uint unknown);

        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr QueryInterface64(uint id);

        [DllImport("nvapi.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr QueryInterface32(uint id);

        static readonly object sync = new object();
        static bool initialized;
        static I2cFn writeEx, readEx;
        static EnumPhysicalGpusFn enumGpus;
        static GetPciIdentifiersFn getPci;
        static GetFullNameFn getFullName;

        static T Get<T>(uint id) where T : class
        {
            IntPtr p = IntPtr.Size == 8 ? QueryInterface64(id) : QueryInterface32(id);
            if (p == IntPtr.Zero)
                return null;
            return (T)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(T));
        }

        static bool Init()
        {
            if (initialized)
                return writeEx != null;
            initialized = true;
            try
            {
                var init = Get<InitializeFn>(0x0150E828);
                if (init == null || init() != 0)
                    return false;
                enumGpus = Get<EnumPhysicalGpusFn>(0xE5AC921F);
                getPci = Get<GetPciIdentifiersFn>(0x2DDFB66E);
                getFullName = Get<GetFullNameFn>(0xCEEE8E9F);
                writeEx = Get<I2cFn>(0x283AC65A);
                readEx = Get<I2cFn>(0x4D7B0709);
                return enumGpus != null && getPci != null && writeEx != null && readEx != null;
            }
            catch (Exception e)
            {
                Log.Write("NVAPI not available: " + e.Message);
                writeEx = null;
                return false;
            }
        }

        public static List<NvGpu> EnumerateGpus()
        {
            var list = new List<NvGpu>();
            lock (sync)
            {
                if (!Init())
                    return list;
                var handles = new IntPtr[64];
                int count;
                if (enumGpus(handles, out count) != 0)
                    return list;
                for (int i = 0; i < count; i++)
                {
                    uint dev, sub, rev, ext;
                    if (getPci(handles[i], out dev, out sub, out rev, out ext) != 0)
                        continue;
                    var g = new NvGpu();
                    g.Handle = handles[i];
                    g.DeviceId = (ushort)(dev >> 16);
                    g.SubsystemVendorId = (ushort)(sub & 0xFFFF);
                    g.SubsystemDeviceId = (ushort)(sub >> 16);
                    g.Name = "GPU";
                    if (getFullName != null)
                    {
                        var name = new byte[64];
                        if (getFullName(handles[i], name) == 0)
                            g.Name = System.Text.Encoding.ASCII.GetString(name).TrimEnd('\0');
                    }
                    list.Add(g);
                }
            }
            return list;
        }

        // SMBus read byte data: returns the byte or -1.
        public static int ReadByteData(IntPtr gpu, byte address, byte command)
        {
            var data = new byte[1];
            return Transfer(gpu, address, command, data, false) ? data[0] : -1;
        }

        public static bool WriteByteData(IntPtr gpu, byte address, byte command, byte value)
        {
            return Transfer(gpu, address, command, new[] { value }, true);
        }

        // SMBus write word data (low byte first).
        public static bool WriteWordData(IntPtr gpu, byte address, byte command, ushort value)
        {
            return Transfer(gpu, address, command, new[] { (byte)value, (byte)(value >> 8) }, true);
        }

        // SMBus block write: length byte followed by the data.
        public static bool WriteBlockData(IntPtr gpu, byte address, byte command, byte[] block)
        {
            var data = new byte[block.Length + 1];
            data[0] = (byte)block.Length;
            Array.Copy(block, 0, data, 1, block.Length);
            return Transfer(gpu, address, command, data, true);
        }

        static bool Transfer(IntPtr gpu, byte address, byte command, byte[] data, bool write)
        {
            lock (sync)
            {
                if (!Init())
                    return false;
                var reg = GCHandle.Alloc(new[] { command }, GCHandleType.Pinned);
                var buf = GCHandle.Alloc(data, GCHandleType.Pinned);
                try
                {
                    var info = new NV_I2C_INFO_V3();
                    info.version = (uint)((3 << 16) | Marshal.SizeOf(typeof(NV_I2C_INFO_V3)));
                    info.i2c_dev_address = (byte)(address << 1);
                    info.i2c_reg_address = reg.AddrOfPinnedObject();
                    info.reg_addr_size = 1;
                    info.data = buf.AddrOfPinnedObject();
                    info.size = (uint)data.Length;
                    info.i2c_speed = 0xFFFF;
                    info.port_id = 1;
                    info.is_port_id_set = 1;
                    uint unknown = 0;
                    return (write ? writeEx : readEx)(gpu, ref info, ref unknown) == 0;
                }
                finally
                {
                    reg.Free();
                    buf.Free();
                }
            }
        }
    }
}
