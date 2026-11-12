// Hardware drivers. Protocol facts follow the OpenRGB project's documentation/source:
//  - ASUS Aura USB mainboard controller (HID, VID 0x0B05, 65-byte reports, id 0xEC)
//  - ENE SMBus RGB controller on ASUS graphics cards (I2C 0x67 via NVAPI)
// Colors passed to Write() are 0x00BBGGRR, one per LED.

using System;
using System.Collections.Generic;
using System.Text;

namespace IcueAuraBridge
{
    internal abstract class HardwareDevice
    {
        public uint AuraType;
        public string Name;
        public string[] LedNames;

        public abstract void EnterDirectMode();
        public abstract void Write(uint[] colors);
        public virtual void Close() { }

        internal static string[] Numbered(string prefix, int count)
        {
            var names = new string[count];
            for (int i = 0; i < count; i++)
                names[i] = prefix + " " + (i + 1);
            return names;
        }
    }

    // One HID connection to the AURA LED Controller, shared by the fixed mainboard
    // zone and the addressable headers.
    internal sealed class AuraUsbController
    {
        static readonly ushort[] MainboardPids = { 0x18F3, 0x1939, 0x19AF, 0x1AA6, 0x1BED };

        const byte ReportId = 0xEC;
        const byte CmdFirmware = 0x82;
        const byte CmdConfigTable = 0xB0;
        const byte CmdEffect = 0x35;
        const byte CmdDirect = 0x40;
        const byte DirectApply = 0x80;
        const byte ModeDirect = 0xFF;
        const int MaxLedsPerPacket = 20;

        readonly HidDeviceInfo info;
        HidDevice hid;
        public string Firmware = "";
        public int AddressableHeaders;
        public int FixedLeds;
        public int RgbHeaders;

        AuraUsbController(HidDeviceInfo info)
        {
            this.info = info;
        }

        public static List<HardwareDevice> Detect(Config config)
        {
            var result = new List<HardwareDevice>();
            foreach (var info in HidDevice.Enumerate(0x0B05))
            {
                if (Array.IndexOf(MainboardPids, info.ProductId) < 0 || info.OutputReportLength < 65 || info.UsagePage != 0xFF72)
                    continue;
                var c = new AuraUsbController(info);
                try
                {
                    c.Open();
                    if (!c.ReadConfig())
                    {
                        c.Close();
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Log.Write("Aura USB " + info.ProductId.ToString("X4") + ": " + e.Message);
                    c.Close();
                    continue;
                }
                Log.Write("Aura USB mainboard controller " + info.ProductId.ToString("X4") + " firmware " + c.Firmware +
                          ": " + c.FixedLeds + " fixed LEDs (" + c.RgbHeaders + " RGB headers), " +
                          c.AddressableHeaders + " ARGB headers");

                string board = BoardName();
                if (c.FixedLeds > 0)
                {
                    var names = new string[c.FixedLeds];
                    int onboard = c.FixedLeds - c.RgbHeaders;
                    for (int i = 0; i < c.FixedLeds; i++)
                        names[i] = i < onboard ? "Mainboard LED " + (i + 1) : "RGB Header " + (i - onboard + 1);
                    result.Add(new AuraUsbZone(c, board, AuraDeviceType.Motherboard, names, 0x04, c.MainboardEffectChannel()));
                }
                for (int h = 0; h < c.AddressableHeaders; h++)
                {
                    int leds = config.ArgbHeaderLeds(h + 1);
                    if (leds <= 0)
                        continue;
                    result.Add(new AuraUsbZone(c, board + " ARGB Header " + (h + 1), AuraDeviceType.MotherboardLedStrip,
                                               HardwareDevice.Numbered("ARGB LED", leds), (byte)h, c.HeaderEffectChannel(h)));
                }
            }
            return result;
        }

        static string BoardName()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                {
                    var s = k == null ? null : k.GetValue("BaseBoardProduct") as string;
                    if (!string.IsNullOrEmpty(s))
                        return s.Trim();
                }
            }
            catch
            {
            }
            return "ASUS Mainboard";
        }

        // Effect channel numbering differs on AR32 firmware.
        bool IsAr32 { get { return Firmware.StartsWith("AULA3-AR32"); } }
        byte MainboardEffectChannel() { return (byte)(IsAr32 ? AddressableHeaders : 0); }
        byte HeaderEffectChannel(int header) { return (byte)(IsAr32 ? header : header + 1); }

        void Open()
        {
            if (hid == null)
                hid = HidDevice.Open(info);
        }

        public void Close()
        {
            if (hid != null)
                hid.Dispose();
            hid = null;
        }

        public void Reopen()
        {
            Close();
            Open();
        }

        bool ReadConfig()
        {
            byte[] fw = Request(CmdFirmware, 0x02);
            if (fw != null)
                Firmware = Encoding.ASCII.GetString(fw, 2, 16).TrimEnd('\0');
            byte[] cfg = Request(CmdConfigTable, 0x30);
            if (cfg == null)
                return false;
            // config_table[i] = reply[i + 4]
            AddressableHeaders = cfg[4 + 0x02];
            FixedLeds = cfg[4 + 0x1B];
            RgbHeaders = cfg[4 + 0x1D];
            if (FixedLeds < RgbHeaders)
                RgbHeaders = 0;
            return true;
        }

        byte[] Request(byte command, byte expectedReply)
        {
            Open();
            hid.Write(new byte[] { ReportId, command });
            for (int tries = 0; tries < 4; tries++)
            {
                byte[] r = hid.Read(500);
                if (r == null)
                    return null;
                if (r[0] == ReportId && r[1] == expectedReply)
                    return r;
            }
            return null;
        }

        public void SetDirectEffect(byte effectChannel)
        {
            Open();
            hid.Write(new byte[] { ReportId, CmdEffect, effectChannel, 0x00, 0x00, ModeDirect });
        }

        public void SendDirect(byte channel, uint[] colors)
        {
            Open();
            for (int offset = 0; offset < colors.Length; offset += MaxLedsPerPacket)
            {
                int count = Math.Min(MaxLedsPerPacket, colors.Length - offset);
                bool last = offset + count >= colors.Length;
                var buf = new byte[5 + 3 * count];
                buf[0] = ReportId;
                buf[1] = CmdDirect;
                buf[2] = (byte)(channel | (last ? DirectApply : 0));
                buf[3] = (byte)offset;
                buf[4] = (byte)count;
                for (int i = 0; i < count; i++)
                {
                    uint c = colors[offset + i];
                    buf[5 + 3 * i] = (byte)c;           // R
                    buf[6 + 3 * i] = (byte)(c >> 8);    // G
                    buf[7 + 3 * i] = (byte)(c >> 16);   // B
                }
                hid.Write(buf);
            }
        }
    }

    internal sealed class AuraUsbZone : HardwareDevice
    {
        readonly AuraUsbController controller;
        readonly byte directChannel;
        readonly byte effectChannel;

        public AuraUsbZone(AuraUsbController controller, string name, uint auraType, string[] leds, byte directChannel, byte effectChannel)
        {
            this.controller = controller;
            Name = name;
            AuraType = auraType;
            LedNames = leds;
            this.directChannel = directChannel;
            this.effectChannel = effectChannel;
        }

        public override void EnterDirectMode()
        {
            controller.SetDirectEffect(effectChannel);
        }

        public override void Write(uint[] colors)
        {
            controller.SendDirect(directChannel, colors);
        }

        public override void Close()
        {
            controller.Reopen();
        }
    }

    // ENE controller on ASUS GPUs. 16-bit registers are selected by a byte-swapped word
    // write to command 0x00; data is read at 0x81, written at 0x01 or block-written at 0x03.
    internal sealed class EneGpu : HardwareDevice
    {
        const byte EneAddress = 0x67;
        const ushort RegDeviceName = 0x1000;
        const ushort RegConfigTable = 0x1C00;
        const ushort RegColorsDirectV1 = 0x8000;
        const ushort RegColorsDirectV2 = 0x8100;
        const ushort RegDirect = 0x8020;
        const ushort RegApply = 0x80A0;
        const byte ApplyVal = 0x01;

        // Controllers that use the v2 register map, and those whose LED count is at config[0x03].
        static readonly string[] V2Versions = { "AUDA0-E6K5-0101", "AUMA0-E6K5-0106", "AUMA0-E6K5-0105", "AUMA0-E6K5-0104",
            "AUMA0-E6K5-0107", "AUMA0-E6K5-1110", "AUMA0-E6K5-1111", "AUMA0-E6K5-1107", "AUMA0-E6K5-1113", "AUMA0-E6K5-1114",
            "AUMA0-E6K5-0008" };
        static readonly string[] LedCountAt3 = { "AUMA0-E6K5-0107", "AUMA0-E6K5-1110", "AUMA0-E6K5-1111", "AUMA0-E6K5-1107",
            "AUMA0-E6K5-1113", "AUMA0-E6K5-1114", "AUMA0-E6K5-0008" };

        readonly IntPtr gpu;
        readonly ushort directReg;
        bool blockWrites = true;

        EneGpu(NvGpu g, string version, int leds)
        {
            gpu = g.Handle;
            directReg = Array.IndexOf(V2Versions, version) >= 0 ? RegColorsDirectV2 : RegColorsDirectV1;
            AuraType = AuraDeviceType.Gpu;
            Name = g.Name.StartsWith("NVIDIA ") ? g.Name.Substring(7) : g.Name;
            LedNames = Numbered("GPU LED", leds);
        }

        public static List<HardwareDevice> Detect(Config config)
        {
            var result = new List<HardwareDevice>();
            foreach (var g in NvApiI2c.EnumerateGpus())
            {
                string id = g.DeviceId.ToString("X4") + " / " + g.SubsystemVendorId.ToString("X4") + ":" + g.SubsystemDeviceId.ToString("X4");
                if (g.SubsystemVendorId != 0x1043)
                    continue;
                if (!GpuList.Contains(g.DeviceId, g.SubsystemVendorId, g.SubsystemDeviceId))
                {
                    if (!config.GpuAllowUnlisted)
                    {
                        Log.Write("GPU " + g.Name + " (" + id + ") is not in the list of known ENE cards; skipped " +
                                  "(set gpu_allow_unlisted = true to try anyway)");
                        continue;
                    }
                    Log.Write("GPU " + g.Name + " (" + id + ") is not in the known list; probing because gpu_allow_unlisted = true");
                }
                if (!Signature(g.Handle))
                {
                    Log.Write("GPU " + g.Name + " (" + id + "): no ENE controller found at 0x67");
                    continue;
                }
                var sb = new StringBuilder();
                for (int i = 0; i < 15; i++)
                {
                    int b = Read(g.Handle, (ushort)(RegDeviceName + i));
                    if (b <= 0) break;
                    sb.Append((char)b);
                }
                string version = sb.ToString();
                int leds = Read(g.Handle, (ushort)(RegConfigTable + (Array.IndexOf(LedCountAt3, version) >= 0 ? 0x03 : 0x02)));
                Log.Write("ENE GPU controller on " + g.Name + " (" + id + "): " + version + ", " + leds + " LEDs");
                if (leds <= 0 || leds > 64)
                    continue;
                result.Add(new EneGpu(g, version, leds));
            }
            return result;
        }

        // ENE controllers return 0x00..0x0F when reading commands 0xA0..0xAF.
        static bool Signature(IntPtr gpu)
        {
            for (int i = 0; i < 16; i++)
                if (NvApiI2c.ReadByteData(gpu, EneAddress, (byte)(0xA0 + i)) != i)
                    return false;
            return true;
        }

        static bool Select(IntPtr gpu, ushort reg)
        {
            return NvApiI2c.WriteWordData(gpu, EneAddress, 0x00, (ushort)(((reg << 8) & 0xFF00) | ((reg >> 8) & 0x00FF)));
        }

        static int Read(IntPtr gpu, ushort reg)
        {
            if (!Select(gpu, reg))
                return -1;
            return NvApiI2c.ReadByteData(gpu, EneAddress, 0x81);
        }

        void WriteRegister(ushort reg, byte value)
        {
            if (!Select(gpu, reg) || !NvApiI2c.WriteByteData(gpu, EneAddress, 0x01, value))
                throw new System.IO.IOException("I2C write to ENE register 0x" + reg.ToString("X4") + " failed");
        }

        public override void EnterDirectMode()
        {
            WriteRegister(RegDirect, 0x01);
            WriteRegister(RegApply, ApplyVal);
        }

        public override void Write(uint[] colors)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                uint c = colors[i];
                var rbg = new[] { (byte)c, (byte)(c >> 16), (byte)(c >> 8) };   // R, B, G
                ushort reg = (ushort)(directReg + 3 * i);
                if (blockWrites)
                {
                    if (Select(gpu, reg) && NvApiI2c.WriteBlockData(gpu, EneAddress, 0x03, rbg))
                        continue;
                    blockWrites = false;
                    Log.Write("ENE block write not supported, falling back to byte writes");
                }
                for (int b = 0; b < 3; b++)
                    WriteRegister((ushort)(reg + b), rbg[b]);
            }
        }
    }
}
