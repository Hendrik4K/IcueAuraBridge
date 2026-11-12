using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IcueAuraBridge
{
    // Optional %ProgramData%\IcueAuraBridge\config.ini, "key = value" lines:
    //   motherboard = true          expose the mainboard's fixed LEDs
    //   gpu = true                  expose the graphics card
    //   argb_header_1_leds = 0      LEDs on ARGB header 1 (0 = header not shown in iCUE)
    //   gpu_allow_unlisted = false  also probe ASUS cards that are not in GpuList (at your own risk)
    internal sealed class Config
    {
        public bool Motherboard = true;
        public bool Gpu = true;
        public bool GpuAllowUnlisted;
        readonly Dictionary<int, int> argbLeds = new Dictionary<int, int>();

        public int ArgbHeaderLeds(int header)
        {
            int n;
            return argbLeds.TryGetValue(header, out n) ? Math.Max(0, Math.Min(n, 240)) : 0;
        }

        public static Config Load(string path)
        {
            var c = new Config();
            try
            {
                if (!File.Exists(path))
                    return c;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';' || line[0] == '[')
                        continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0)
                        continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();
                    if (key == "motherboard")
                        c.Motherboard = ParseBool(val);
                    else if (key == "gpu")
                        c.Gpu = ParseBool(val);
                    else if (key == "gpu_allow_unlisted")
                        c.GpuAllowUnlisted = ParseBool(val);
                    else if (key.StartsWith("argb_header_") && key.EndsWith("_leds"))
                        c.argbLeds[int.Parse(key.Substring(12, key.Length - 17))] = int.Parse(val);
                }
            }
            catch (Exception e)
            {
                Log.Write("Could not read config: " + e.Message);
            }
            return c;
        }

        static bool ParseBool(string s)
        {
            s = s.ToLowerInvariant();
            return s == "1" || s == "true" || s == "yes" || s == "on";
        }
    }

    internal static class Log
    {
        static readonly object sync = new object();
        static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "IcueAuraBridge", "bridge.log");

        public static void Write(string message)
        {
            lock (sync)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogFile));
                    var fi = new FileInfo(LogFile);
                    if (fi.Exists && fi.Length > 1024 * 1024)
                        File.Copy(LogFile, LogFile + ".old", true);
                    if (fi.Exists && fi.Length > 1024 * 1024)
                        File.Delete(LogFile);
                    File.AppendAllText(LogFile,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + "\r\n", Encoding.UTF8);
                }
                catch
                {
                    // Logging must never break iCUE.
                }
            }
        }
    }
}
