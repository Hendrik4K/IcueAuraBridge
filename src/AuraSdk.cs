// COM objects handed to iCUE. Only AuraSdk is creatable (CLSID/ProgID of the real
// ASUS SDK); everything else is returned from it. Constructors of the other classes
// are internal so RegAsm does not register them.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IcueAuraBridge
{
    [ComVisible(true)]
    [Guid(AuraIds.ClsidAuraSdk)]
    [ProgId(AuraIds.ProgId)]
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(IAuraSdk))]
    public sealed class AuraSdk : IAuraSdk, IAuraSdk2
    {
        public AuraSdk()
        {
            Log.Write("AuraSdk created in process " + System.Diagnostics.Process.GetCurrentProcess().Id +
                      " as " + Environment.UserName);
        }

        public IAuraSyncDeviceCollection Enumerate(uint devType)
        {
            var result = new List<IAuraSyncDevice>();
            try
            {
                foreach (var d in Bridge.GetDevices())
                    if (devType == AuraDeviceType.All || devType == d.AuraType)
                        result.Add(new AuraSyncDevice(d));
                Log.Write("Enumerate(0x" + devType.ToString("X8") + ") -> " + result.Count + " device(s)");
            }
            catch (Exception e)
            {
                Log.Write("Enumerate failed: " + e);
            }
            return new AuraSyncDeviceCollection(result);
        }

        public void SwitchMode()
        {
            Bridge.RequestDirectMode();
        }

        public void ReleaseControl(uint reserve)
        {
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class AuraSyncDeviceCollection : IAuraSyncDeviceCollection
    {
        readonly List<IAuraSyncDevice> items;

        internal AuraSyncDeviceCollection(List<IAuraSyncDevice> items)
        {
            this.items = items;
        }

        public object get__NewEnum() { return null; }

        public int get_Count() { return items.Count; }

        public IAuraSyncDevice get_Item(int index)
        {
            if (index < 0 || index >= items.Count)
                throw new ArgumentOutOfRangeException("index");
            return items[index];
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class AuraSyncDevice : IAuraSyncDevice
    {
        readonly BridgeDevice device;
        readonly AuraRgbLightCollection lights;

        internal AuraSyncDevice(BridgeDevice device)
        {
            this.device = device;
            var list = new List<IAuraRgbLight>(device.LedNames.Length);
            for (int i = 0; i < device.LedNames.Length; i++)
                list.Add(new AuraRgbLight(device, i));
            lights = new AuraRgbLightCollection(list);
        }

        public IAuraRgbLightCollection get_Lights() { return lights; }
        public uint get_Type() { return device.AuraType; }
        public string get_Name() { return device.Name; }
        public uint get_Width() { return (uint)device.LedNames.Length; }
        public uint get_Height() { return 1; }

        public void Apply()
        {
            Bridge.Submit(device);
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class AuraRgbLightCollection : IAuraRgbLightCollection
    {
        readonly List<IAuraRgbLight> items;

        internal AuraRgbLightCollection(List<IAuraRgbLight> items)
        {
            this.items = items;
        }

        public object get__NewEnum() { return null; }

        public int get_Count() { return items.Count; }

        public IAuraRgbLight get_Item(int index)
        {
            if (index < 0 || index >= items.Count)
                throw new ArgumentOutOfRangeException("index");
            return items[index];
        }
    }

    // Color layout is 0x00BBGGRR (bits 0-7 red, 8-15 green, 16-23 blue).
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class AuraRgbLight : IAuraRgbLight
    {
        readonly BridgeDevice device;
        readonly int index;

        internal AuraRgbLight(BridgeDevice device, int index)
        {
            this.device = device;
            this.index = index;
        }

        public byte get_Red() { return (byte)device.Colors[index]; }
        public void put_Red(byte value) { SetByte(0, value); }
        public byte get_Green() { return (byte)(device.Colors[index] >> 8); }
        public void put_Green(byte value) { SetByte(8, value); }
        public byte get_Blue() { return (byte)(device.Colors[index] >> 16); }
        public void put_Blue(byte value) { SetByte(16, value); }
        public string get_Name() { return device.LedNames[index]; }
        public uint get_Color() { return device.Colors[index]; }
        public void put_Color(uint value) { device.Colors[index] = value & 0x00FFFFFF; }

        void SetByte(int shift, byte value)
        {
            uint c = device.Colors[index];
            device.Colors[index] = (c & ~(0xFFu << shift)) | ((uint)value << shift);
        }
    }
}
