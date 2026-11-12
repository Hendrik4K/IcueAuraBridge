// Backend shared by all COM objects: hardware detection and the color sender.
//
// iCUE calls Apply() from its lighting loop, so Apply() only stores the frame and wakes
// a background thread. That thread owns all hardware I/O, sends only frames that
// changed, refreshes every few seconds, and re-initialises direct mode after errors
// or a sleep/resume gap. iCUE never blocks on USB or I2C.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace IcueAuraBridge
{
    internal sealed class BridgeDevice
    {
        public HardwareDevice Hardware;
        public uint AuraType { get { return Hardware.AuraType; } }
        public string Name { get { return Hardware.Name; } }
        public string[] LedNames { get { return Hardware.LedNames; } }
        public uint[] Colors;        // 0x00BBGGRR, written by iCUE through IAuraRgbLight

        // Sender state
        public uint[] PendingFrame;
        public uint[] LastSent;
        public bool DirectModeSet;
        public DateTime RetryAfter;  // error back-off
        public DateTime LastWrite;
        public int Writes;
    }

    internal static class Bridge
    {
        static readonly object sync = new object();
        static readonly AutoResetEvent wake = new AutoResetEvent(false);
        static List<BridgeDevice> devices;
        static Thread sender;

        public static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "IcueAuraBridge");

        public static readonly Config Config = Config.Load(Path.Combine(DataDir, "config.ini"));

        const int RefreshMs = 5000;
        const int RetryMs = 5000;

        public static List<BridgeDevice> GetDevices()
        {
            lock (sync)
            {
                if (devices == null)
                {
                    devices = Detect();
                    sender = new Thread(SenderLoop);
                    sender.IsBackground = true;
                    sender.Name = "IcueAuraBridge sender";
                    sender.Start();
                }
                return devices;
            }
        }

        public static void Submit(BridgeDevice d)
        {
            lock (sync)
                d.PendingFrame = (uint[])d.Colors.Clone();
            wake.Set();
        }

        public static void RequestDirectMode()
        {
            lock (sync)
            {
                if (devices != null)
                    foreach (var d in devices)
                        d.DirectModeSet = false;
            }
            wake.Set();
        }

        static List<BridgeDevice> Detect()
        {
            var hw = new List<HardwareDevice>();
            if (Config.Motherboard)
                Try("mainboard", delegate { hw.AddRange(AuraUsbController.Detect(Config)); });
            if (Config.Gpu)
                Try("GPU", delegate { hw.AddRange(EneGpu.Detect(Config)); });

            var list = new List<BridgeDevice>();
            foreach (var h in hw)
            {
                var d = new BridgeDevice();
                d.Hardware = h;
                d.Colors = new uint[h.LedNames.Length];
                list.Add(d);
                Log.Write("Device: " + (h.AuraType == AuraDeviceType.Gpu ? "GPU" : "Mainboard") + " '" + h.Name + "', " + h.LedNames.Length + " LEDs");
            }
            return list;
        }

        static void Try(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Write("Detecting " + what + " failed: " + e);
            }
        }

        static void SenderLoop()
        {
            DateTime lastLoop = DateTime.UtcNow;
            while (true)
            {
                wake.WaitOne(1000);

                // A long gap means the system slept; controllers may have reset to their own effects.
                DateTime now = DateTime.UtcNow;
                if ((now - lastLoop).TotalSeconds > 20)
                {
                    Log.Write("Resume detected, re-initialising direct mode");
                    RequestDirectMode();
                }
                lastLoop = now;

                List<BridgeDevice> list;
                lock (sync)
                    list = new List<BridgeDevice>(devices);

                foreach (var d in list)
                {
                    if (now < d.RetryAfter)
                        continue;
                    uint[] frame;
                    lock (sync)
                    {
                        frame = d.PendingFrame;
                        d.PendingFrame = null;
                    }
                    if (frame == null && d.LastSent == null)
                        continue;   // iCUE has not sent anything for this device yet
                    if (frame == null)
                        frame = d.LastSent;

                    try
                    {
                        bool init = !d.DirectModeSet;
                        if (init)
                        {
                            d.Hardware.EnterDirectMode();
                            d.DirectModeSet = true;
                        }
                        bool changed = d.LastSent == null || !Same(frame, d.LastSent);
                        bool refresh = (now - d.LastWrite).TotalMilliseconds > RefreshMs;
                        if (init || changed || refresh)
                        {
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            d.Hardware.Write(frame);
                            if (d.Writes++ < 3)
                                Log.Write("'" + d.Name + "': frame written in " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + " ms" +
                                          (init ? " (after entering direct mode)" : ""));
                            d.LastSent = frame;
                            d.LastWrite = now;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Write("'" + d.Name + "': " + e.Message + " - retrying in " + RetryMs / 1000 + " s");
                        d.DirectModeSet = false;
                        d.RetryAfter = now.AddMilliseconds(RetryMs);
                        try { d.Hardware.Close(); } catch { }
                    }
                }
            }
        }

        static bool Same(uint[] a, uint[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }
    }
}
