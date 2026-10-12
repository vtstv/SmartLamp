// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Threading.Tasks;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        // Legacy daemon stubs - no background Python processes needed in pure C#
        public Task WaitForBridgeReadyAsync() => Task.CompletedTask;

        public static void StopDaemon()
        {
            // No daemon to stop in pure C#
        }
    }
}
