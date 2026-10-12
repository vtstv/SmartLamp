// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

namespace TuyaNet.Native.Models
{
    /// <summary>
    /// Identifiers for Tuya protocol message command codes.
    /// </summary>
    public enum TuyaCommandType : uint
    {
        /// <summary>UDP Discovery Request (App to Device, port 7000).</summary>
        ReqDevInfo = 0x01,

        /// <summary>Session Key Negotiation Step 1 (Client nonce).</summary>
        SessKeyNegStart = 0x03,

        /// <summary>Session Key Negotiation Step 2 (Device nonce and HMAC).</summary>
        SessKeyNegResp = 0x04,

        /// <summary>Session Key Negotiation Step 3 (Client HMAC of device nonce).</summary>
        SessKeyNegFinish = 0x05,

        /// <summary>Device Control (v3.1, v3.3, v3.4).</summary>
        Control = 0x07,

        /// <summary>Device Status response / push event.</summary>
        Status = 0x08,

        /// <summary>Connection Keep-alive Heartbeat.</summary>
        Heartbeat = 0x09,

        /// <summary>Query data points status (v3.3).</summary>
        DpQuery = 0x0A,

        /// <summary>Device Control New (v3.5 GCM framed).</summary>
        ControlNew = 0x0D,

        /// <summary>Query data points status New (v3.5 GCM framed).</summary>
        DpQueryNew = 0x10,

        /// <summary>UDP Discovery Broadcast (Device to App, ports 6666/6667).</summary>
        Udp = 0x23
    }
}
