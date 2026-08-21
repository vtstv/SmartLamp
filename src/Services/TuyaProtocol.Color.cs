// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        private static string RgbToTuyaV2Hex(int r, int g, int b)
        {
            float rF = r / 255.0f;
            float gF = g / 255.0f;
            float bF = b / 255.0f;

            float maxC = Math.Max(rF, Math.Max(gF, bF));
            float minC = Math.Min(rF, Math.Min(gF, bF));
            float delta = maxC - minC;

            float h = 0f;
            if (delta > 0.00001f)
            {
                if (maxC == rF) h = (gF - bF) / delta % 6f;
                else if (maxC == gF) h = (bF - rF) / delta + 2f;
                else h = (rF - gF) / delta + 4f;
                h *= 60f;
                if (h < 0f) h += 360f;
            }

            float s = maxC == 0f ? 0f : delta / maxC;
            float v = maxC;

            int hVal = (int)Math.Round(h);
            int sVal = (int)Math.Round(s * 1000.0f);
            int vVal = (int)Math.Round(v * 1000.0f);

            return $"{hVal:x4}{sVal:x4}{vVal:x4}";
        }

        public static string TuyaHsvToRgbHex(int h, int s, int v)
        {
            float hF = (h % 360) / 60.0f;
            float sF = Math.Clamp(s / 1000.0f, 0f, 1f);
            float vF = Math.Clamp(v / 1000.0f, 0f, 1f);

            int i = (int)Math.Floor(hF);
            float f = hF - i;
            float p = vF * (1.0f - sF);
            float q = vF * (1.0f - sF * f);
            float t = vF * (1.0f - sF * (1.0f - f));

            float rF = 0, gF = 0, bF = 0;
            switch (i)
            {
                case 0: rF = vF; gF = t; bF = p; break;
                case 1: rF = q; gF = vF; bF = p; break;
                case 2: rF = p; gF = vF; bF = t; break;
                case 3: rF = p; gF = q; bF = vF; break;
                case 4: rF = t; gF = p; bF = vF; break;
                default: rF = vF; gF = p; bF = q; break;
            }

            int r = Math.Clamp((int)Math.Round(rF * 255.0f), 0, 255);
            int g = Math.Clamp((int)Math.Round(gF * 255.0f), 0, 255);
            int b = Math.Clamp((int)Math.Round(bF * 255.0f), 0, 255);

            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
