// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Text;
using System.Text.Json;
using TuyaNet.Native.Crypto;
using TuyaNet.Native.Framing;
using TuyaNet.Native.Models;
using TuyaNet.Native.Session;

namespace TuyaNet.Native
{
    /// <summary>
    /// High-level client for communicating with a physical Tuya device over the local network.
    /// Provides zero-dependency, ultra-fast LAN control directly over TCP port 6668.
    /// </summary>
    public class TuyaDevice : IDisposable
    {
        public const int DefaultPort = 6668;

        private readonly string _ip;
        private readonly int _port;
        private readonly string _devId;
        private readonly byte[] _localKeyBytes;
        private readonly TuyaProtocolVersion _version;
        private readonly TuyaSession _session;

        /// <summary>
        /// Creates a new instance of <see cref="TuyaDevice"/>.
        /// </summary>
        /// <param name="ip">IP address of the device on the local network.</param>
        /// <param name="devId">Unique Device ID (gwId).</param>
        /// <param name="localKey">16-byte local encryption key.</param>
        /// <param name="version">Protocol version (defaults to v3.5).</param>
        /// <param name="port">TCP port (defaults to 6668).</param>
        public TuyaDevice(string ip, string devId, string localKey, TuyaProtocolVersion version = TuyaProtocolVersion.V35, int port = DefaultPort)
        {
            _ip = ip;
            _port = port > 0 ? port : DefaultPort;
            _devId = devId;
            _localKeyBytes = Encoding.UTF8.GetBytes(localKey);
            _version = version;
            _session = new TuyaSession(_ip, _port, _localKeyBytes, _version);
        }

        /// <summary>
        /// Creates a new instance using string version representation (e.g. "3.5", "3.3").
        /// </summary>
        public TuyaDevice(string ip, string devId, string localKey, string version, int port = DefaultPort)
            : this(ip, devId, localKey, TuyaProtocolVersionExtensions.ParseVersion(version), port)
        {
        }

        public string Ip => _ip;
        public string DeviceId => _devId;
        public TuyaProtocolVersion Version => _version;
        public bool IsConnected => _session.IsConnected;

        #region Status Query

        /// <summary>
        /// Queries the current DPS state of the device and returns raw JSON (e.g. {"dps":{"20":true,...}}).
        /// </summary>
        public async Task<string> GetStatusJsonAsync(CancellationToken cancellationToken = default)
        {
            if (_session.IsVersion35)
            {
                byte[] payload = Encoding.UTF8.GetBytes("{}");
                var (_, plaintext) = await _session.SendReceive6699Async(TuyaCommandType.DpQueryNew, payload, cancellationToken).ConfigureAwait(false);
                return TuyaHeader.ExtractJsonString(plaintext);
            }
            else
            {
                long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string queryJson = $"{{\"gwId\":\"{_devId}\",\"devId\":\"{_devId}\",\"uid\":\"{_devId}\",\"t\":\"{t}\"}}";
                byte[] encPayload = TuyaCrypto.AesEcbEncrypt(Encoding.UTF8.GetBytes(queryJson), _localKeyBytes);
                var (_, body) = await _session.SendReceive55AAAsync(TuyaCommandType.DpQuery, encPayload, cancellationToken).ConfigureAwait(false);
                byte[] decrypted = TuyaCrypto.AesEcbDecrypt(body, _localKeyBytes);
                return TuyaHeader.ExtractJsonString(decrypted);
            }
        }

        #endregion

        #region Set DPS

        /// <summary>
        /// Updates one or multiple DPS values on the device.
        /// </summary>
        public async Task<bool> SetDpsAsync(Dictionary<string, object> dps, CancellationToken cancellationToken = default)
        {
            if (dps == null || dps.Count == 0) return true;

            if (_session.IsVersion35)
            {
                long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var dataObj = new
                {
                    protocol = 5,
                    t = t,
                    data = new { dps = dps }
                };
                string jsonStr = JsonSerializer.Serialize(dataObj);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonStr);

                byte[] fullPayload = new byte[TuyaHeader.Version35Header.Length + jsonBytes.Length];
                Buffer.BlockCopy(TuyaHeader.Version35Header, 0, fullPayload, 0, TuyaHeader.Version35Header.Length);
                Buffer.BlockCopy(jsonBytes, 0, fullPayload, TuyaHeader.Version35Header.Length, jsonBytes.Length);

                var (respCmd, plaintext) = await _session.SendReceive6699Async(TuyaCommandType.ControlNew, fullPayload, cancellationToken).ConfigureAwait(false);
                if (plaintext == null || plaintext.Length == 0)
                {
                    return respCmd == TuyaCommandType.ControlNew || respCmd == TuyaCommandType.Status;
                }

                if (plaintext.Length >= 4)
                {
                    uint returnCode = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(plaintext.AsSpan(0, 4));
                    if (returnCode == 0)
                    {
                        string respJson = TuyaHeader.ExtractJsonString(plaintext);
                        return string.IsNullOrWhiteSpace(respJson) || !respJson.Contains("\"error\"");
                    }
                    return false;
                }

                string json = TuyaHeader.ExtractJsonString(plaintext);
                return !string.IsNullOrWhiteSpace(json) && !json.Contains("\"error\"");
            }
            else
            {
                long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var dataObj = new
                {
                    devId = _devId,
                    uid = _devId,
                    t = t.ToString(),
                    dps = dps
                };
                string jsonStr = JsonSerializer.Serialize(dataObj);
                byte[] encPayload = TuyaCrypto.AesEcbEncrypt(Encoding.UTF8.GetBytes(jsonStr), _localKeyBytes);

                byte[] fullPayload = new byte[TuyaHeader.Version33Header.Length + encPayload.Length];
                Buffer.BlockCopy(TuyaHeader.Version33Header, 0, fullPayload, 0, TuyaHeader.Version33Header.Length);
                Buffer.BlockCopy(encPayload, 0, fullPayload, TuyaHeader.Version33Header.Length, encPayload.Length);

                var (respCmd, _) = await _session.SendReceive55AAAsync(TuyaCommandType.Control, fullPayload, cancellationToken).ConfigureAwait(false);
                return respCmd == TuyaCommandType.Status || respCmd == TuyaCommandType.Control;
            }
        }

        /// <summary>
        /// Sets a single DPS value.
        /// </summary>
        public Task<bool> SetDpAsync(string dpId, object value, CancellationToken cancellationToken = default)
        {
            return SetDpsAsync(new Dictionary<string, object> { [dpId] = value }, cancellationToken);
        }

        /// <summary>
        /// Convenience method to set device power state (switch dp).
        /// </summary>
        public Task<bool> SetPowerAsync(bool on, string dpId = "20", CancellationToken cancellationToken = default)
        {
            return SetDpAsync(dpId, on, cancellationToken);
        }

        /// <summary>
        /// Toggles device power state by querying the current state first.
        /// </summary>
        public async Task<bool> TogglePowerAsync(string dpId = "20", CancellationToken cancellationToken = default)
        {
            string statusJson = await GetStatusJsonAsync(cancellationToken).ConfigureAwait(false);
            bool currentPower = statusJson.Contains($"\"{dpId}\":true") || statusJson.Contains($"\"{dpId}\": true");
            return await SetPowerAsync(!currentPower, dpId, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        /// <inheritdoc />
        public void Dispose()
        {
            _session.Dispose();
        }
    }
}
