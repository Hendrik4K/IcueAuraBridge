// NVIDIA cards with an ENE RGB controller at I2C 0x67, as listed by the OpenRGB project
// (Controllers/ENESMBusController/ENESMBusControllerDetect.cpp). Only these cards are
// probed unless "gpu_allow_unlisted = true" is set, because poking the I2C bus of an
// unknown card is not safe. Format: PCI device id, ASUS subsystem device id, model.

namespace IcueAuraBridge
{
    internal static class GpuList
    {
        public static bool Contains(ushort device, ushort subsystemVendor, ushort subsystemDevice)
        {
            if (subsystemVendor != 0x1043)
                return false;
            uint key = ((uint)device << 16) | subsystemDevice;
            foreach (uint k in Known)
                if (k == key)
                    return true;
            return false;
        }

        static readonly uint[] Known =
        {
            // RTX 3050 / 3060
            0x25078872, // ROG STRIX RTX 3050 Gaming
            0x250387FB, // KO RTX 3060 Gaming OC
            0x250387F5, // TUF RTX 3060 Gaming OC
            0x24878865, // TUF RTX 3060 V2 Gaming OC
            0x25048816, // TUF RTX 3060 V2 Gaming OC
            0x250387F4, // ROG STRIX RTX 3060 Gaming
            0x250387F3, // ROG STRIX RTX 3060 Gaming OC
            0x25048818, // ROG STRIX RTX 3060 V2 Gaming OC
            0x25048821, // KO RTX 3060 V2 Gaming OC
            // RTX 3060 Ti
            0x2489883E, // KO RTX 3060 Ti V2 Gaming OC
            0x24C988AC, // TUF RTX 3060 Ti OC
            0x248687CA, // KO RTX 3060 Ti Gaming OC
            0x248687CB, // KO RTX 3060 Ti Gaming OC
            0x248687C6, // TUF RTX 3060 Ti Gaming OC
            0x248687BA, // ROG STRIX RTX 3060 Ti OC
            0x24898827, // TUF RTX 3060 Ti OC
            0x24898834, // ROG STRIX RTX 3060 Ti V2 OC
            0x24898835, // ROG STRIX RTX 3060 Ti V2 OC
            // RTX 3070
            0x248487B8, // ROG STRIX RTX 3070 OC
            0x248487B9, // ROG STRIX RTX 3070 Gaming OC
            0x248487E0, // ROG STRIX RTX 3070 White OC
            0x2488882D, // ROG STRIX RTX 3070 V2 Gaming
            0x2488882C, // ROG STRIX RTX 3070 V2 Gaming OC
            0x24888832, // ROG STRIX RTX 3070 V2 White OC
            0x248487C2, // TUF RTX 3070 Gaming
            0x248487C1, // TUF RTX 3070 Gaming OC
            0x248487BE, // KO RTX 3070 Gaming OC
            0x24888842, // KO RTX 3070 V2 Gaming OC
            0x24888843, // KO RTX 3070 V2 Gaming OC
            0x24888825, // TUF RTX 3070 V2 Gaming OC
            // RTX 3070 Ti
            0x2482880E, // ROG STRIX RTX 3070 Ti Gaming OC
            0x24828812, // TUF RTX 3070 Ti V2 Gaming OC (tested)
            0x220788BD, // TUF RTX 3070 Ti V2 Gaming OC
            0x220788BC, // TUF RTX 3070 Ti V2 Gaming OC
            0x24828813, // TUF RTX 3070 Ti Gaming OC
            // RTX 3080 / 3080 Ti
            0x220687C4, // TUF RTX 3080 Gaming
            0x220687B2, // TUF RTX 3080 Gaming
            0x220687CE, // ROG RTX 3080 GUNDAM EDITION
            0x220687B0, // TUF RTX 3080 Gaming OC
            0x220687AA, // ROG STRIX RTX 3080 Gaming
            0x220687AC, // ROG STRIX RTX 3080 Gaming OC
            0x220687D1, // ROG STRIX RTX 3080 White OC
            0x2216882F, // ROG STRIX RTX 3080 V2 Gaming
            0x2216882E, // ROG STRIX RTX 3080 V2 Gaming OC
            0x22168830, // ROG STRIX RTX 3080 V2 White OC
            0x22168822, // TUF RTX 3080 V2 Gaming OC
            0x2216882B, // TUF RTX 3080 V2 Gaming OC
            0x22168823, // TUF RTX 3080 V2 Gaming OC
            0x220A886F, // TUF RTX 3080 12G Gaming
            0x220A886E, // TUF RTX 3080 12G Gaming OC
            0x220A886C, // ROG STRIX RTX 3080 12G
            0x220A886B, // ROG STRIX RTX 3080 12G OC
            0x220A8887, // ROG STRIX RTX 3080 12G OC EVA EDITION
            0x22088803, // TUF RTX 3080 Ti Gaming
            0x22088802, // TUF RTX 3080 Ti Gaming OC
            0x22088807, // ROG STRIX RTX 3080 Ti Gaming OC
            0x22088809, // ROG STRIX LC RTX 3080 Ti Gaming OC
            // RTX 3090 / 3090 Ti
            0x22048886, // ROG STRIX RTX 3090 OC EVA EDITION
            0x220487CD, // ROG STRIX RTX 3090 GUNDAM EDITION
            0x220487AD, // ROG STRIX RTX 3090 Gaming
            0x220487C5, // ROG STRIX RTX 3090 Gaming
            0x220487AF, // ROG STRIX RTX 3090 Gaming OC
            0x220487D9, // ROG STRIX RTX 3090 Gaming White OC
            0x220487B5, // TUF RTX 3090 Gaming OC
            0x220487B3, // TUF RTX 3090 Gaming OC
            0x22038875, // TUF RTX 3090 Ti Gaming
            0x22038874, // TUF RTX 3090 Ti Gaming OC
            0x22038870, // ROG STRIX LC RTX 3090 Ti Gaming OC
            // RTX 4060 / 4060 Ti
            0x280388F6, // TUF RTX 4060 Ti Gaming OC
            0x28828908, // ROG STRIX RTX 4060 Gaming OC
            0x280388FB, // ROG STRIX RTX 4060 Ti Gaming OC
            // RTX 4070 / SUPER / Ti / Ti SUPER
            0x278688DE, // TUF RTX 4070 Gaming
            0x278688DF, // TUF RTX 4070 Gaming OC
            0x278688EB, // TUF RTX 4070 Gaming OC
            0x278688EC, // TUF RTX 4070 Gaming OC
            0x27838952, // TUF RTX 4070 SUPER Gaming
            0x278688F3, // ROG STRIX RTX 4070 Gaming OC
            0x27838973, // ROG STRIX RTX 4070 SUPER Gaming
            0x27838972, // ROG STRIX RTX 4070 SUPER Gaming OC
            0x278288A4, // TUF RTX 4070 Ti Gaming
            0x278288DD, // TUF RTX 4070 Ti Gaming
            0x278288A3, // TUF RTX 4070 Ti Gaming OC
            0x278288DC, // TUF RTX 4070 Ti Gaming OC
            0x27828935, // TUF RTX 4070 Ti Gaming White OC
            0x27058958, // TUF RTX 4070 Ti SUPER Gaming OC
            0x27058957, // TUF RTX 4070 Ti SUPER Gaming OC
            0x2705895B, // TUF RTX 4070 Ti SUPER Gaming White OC
            0x268989B0, // TUF RTX 4070 Ti SUPER Gaming OC
            0x278288A6, // ROG STRIX RTX 4070 Ti Gaming
            0x278288E5, // ROG STRIX RTX 4070 Ti Gaming OC
            0x278288A7, // ROG STRIX RTX 4070 Ti Gaming OC
            0x2705896B, // ROG STRIX RTX 4070 Ti SUPER Gaming
            0x2705896D, // ROG STRIX RTX 4070 Ti SUPER Gaming OC
            // RTX 4080 / SUPER
            0x270488C0, // ROG STRIX RTX 4080 Gaming
            0x270488C9, // ROG STRIX RTX 4080 Gaming White
            0x270488C8, // ROG STRIX RTX 4080 Gaming White OC
            0x270488BF, // ROG STRIX RTX 4080 Gaming OC
            0x2704889F, // ROG STRIX RTX 4080 Gaming OC
            0x270488A2, // TUF RTX 4080 Gaming
            0x270488CB, // TUF RTX 4080 Gaming
            0x270488CA, // TUF RTX 4080 Gaming OC
            0x270488A1, // TUF RTX 4080 Gaming OC
            0x27028963, // TUF RTX 4080 SUPER Gaming
            0x27028964, // ROG STRIX RTX 4080 SUPER OC
            0x27028969, // ROG STRIX RTX 4080 SUPER White
            0x27028968, // ROG STRIX RTX 4080 SUPER White OC
            0x27028962, // TUF RTX 4080 SUPER Gaming OC
            0x270289C9, // TUF RTX 4080 SUPER Gaming OC
            // RTX 4090
            0x2684889A, // TUF RTX 4090 Gaming OC
            0x268488E6, // TUF RTX 4090 Gaming OG OC
            0x2684889B, // TUF RTX 4090 Gaming OC
            0x268488E2, // TUF RTX 4090 Gaming OC
            0x268488E3, // TUF RTX 4090 Gaming OC
            0x268488E8, // ROG STRIX LC RTX 4090 Gaming OC
            0x2684889D, // ROG STRIX RTX 4090 Gaming
            0x2684889C, // ROG STRIX RTX 4090 Gaming OC
            0x268488EF, // ROG STRIX RTX 4090 Gaming OC
            0x268488F0, // ROG STRIX RTX 4090 Gaming
            0x2684890C, // ROG STRIX RTX 4090 OC EVA-02
            0x26848932, // ROG STRIX RTX 4090 Gaming OC
            0x26848933, // ROG STRIX RTX 4090 Gaming OC
            0x268488C4, // ROG STRIX RTX 4090 Gaming White
            0x268488F2, // ROG STRIX RTX 4090 Gaming White
            0x268488C3, // ROG STRIX RTX 4090 Gaming White OC
            0x268488F1, // ROG STRIX RTX 4090 Gaming White OC
            0x26848934, // ROG MATRIX PLATINUM RTX 4090
            // RTX 50 series
            0x2D058A1A, // TUF RTX 5060 Gaming OC
            0x2D048A08, // TUF RTX 5060 Ti Gaming OC
            0x2F0489F2, // TUF RTX 5070 Gaming OC
            0x2F048A18, // ROG STRIX RTX 5070 Gaming OC
            0x2C0589F4, // TUF RTX 5070 Ti Gaming OC
            0x2C058A37, // TUF RTX 5070 Ti Gaming BTF White OC
            0x2C058A0C, // TUF RTX 5070 Ti Gaming White OC
            0x2C058A0D, // ROG STRIX RTX 5070 Ti Gaming OC
            0x2C0289D7, // TUF RTX 5080 Gaming OC
            0x2C0289DE, // ROG ASTRAL RTX 5080 OC
            0x2C028A2B, // ROG ASTRAL RTX 5080 OC WHITE
            0x2C0289DF, // ROG ASTRAL RTX 5080
            0x2C028A2C, // ROG ASTRAL RTX 5080 WHITE
            0x2B8589EE, // TUF RTX 5090 Gaming OC
            0x2B8589EF, // TUF RTX 5090 Gaming
            0x2B8589E3, // ROG ASTRAL RTX 5090 OC
            0x2B8589E4, // ROG ASTRAL RTX 5090
            0x2B858A3C, // ROG ASTRAL RTX 5090 OC BTF
            0x2B858A5A, // ROG ASTRAL RTX 5090 OC BTF
            0x2B858A2E, // ROG ASTRAL RTX 5090 OC WHITE
            0x2B8589EC, // ROG ASTRAL LC RTX 5090 OC
            0x2B8589ED, // ROG ASTRAL LC OC RTX 5090 OC
            0x2B858A61, // ROG MATRIX PLATINUM RTX 5090
        };
    }
}
