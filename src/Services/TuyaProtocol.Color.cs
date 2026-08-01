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
    }
}
