// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartLampApp.Services
{
    public class MqttService
    {
        private HttpListener? _mqttBridgeListener;
        private bool _isRunning = false;
        private int _port = 1883;
        private readonly TuyaProtocol _protocol;

        public event Action<string>? OnLogMessage;

        public MqttService(TuyaProtocol protocol)
        {
            _protocol = protocol;
        }

        public bool IsRunning => _isRunning;

        public void StartBridge(int port = 1883)
        {
            if (_isRunning) return;
            _port = port;

            try
            {
                _mqttBridgeListener = new HttpListener();
                _mqttBridgeListener.Prefixes.Add($"http://localhost:{_port}/mqtt/");
                _mqttBridgeListener.Prefixes.Add($"http://127.0.0.1:{_port}/mqtt/");
                _mqttBridgeListener.Start();
                _isRunning = true;

                OnLogMessage?.Invoke($"Home Assistant & MQTT REST Bridge listening on port {_port}");
                Task.Run(() => ListenLoop());
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Failed to start MQTT Bridge: {ex.Message}");
                _isRunning = false;
            }
        }

        public void StopBridge()
        {
            _isRunning = false;
            try
            {
                _mqttBridgeListener?.Stop();
                _mqttBridgeListener?.Close();
            }
            catch { }
        }

        private async Task ListenLoop()
        {
            while (_isRunning && _mqttBridgeListener != null && _mqttBridgeListener.IsListening)
            {
                try
                {
                    var context = await _mqttBridgeListener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch { }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 200;
                resp.Close();
                return;
            }

            string responseJson = "";
            try
            {
                using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                string body = await reader.ReadToEndAsync();

                if (req.Url?.AbsolutePath.Contains("discovery") == true || body.Contains("discovery"))
                {
                    responseJson = GetHomeAssistantDiscoveryPayload();
                    OnLogMessage?.Invoke("Home Assistant Auto-Discovery Payload generated.");
                }
                else if (req.Url?.AbsolutePath.Contains("set") == true || req.HttpMethod == "POST")
                {
                    responseJson = await HandleCommandAsync(body);
                }
                else
                {
                    responseJson = JsonSerializer.Serialize(new
                    {
                        status = "online",
                        service = "SmartLamp Studio Home Assistant & MQTT Bridge",
                        discovery_topic = "homeassistant/light/smartlamp_studio/config"
                    });
                }
            }
            catch (Exception ex)
            {
                responseJson = JsonSerializer.Serialize(new { error = ex.Message });
            }

            byte[] buf = Encoding.UTF8.GetBytes(responseJson);
            resp.ContentType = "application/json";
            resp.ContentLength64 = buf.Length;
            await resp.OutputStream.WriteAsync(buf, 0, buf.Length);
            resp.Close();
        }

        public string GetHomeAssistantDiscoveryPayload()
        {
            var active = _protocol.GetActiveDevice();
            var payload = new
            {
                name = active.Name,
                unique_id = $"smartlamp_{active.DevId}",
                cmd_t = $"smartlamp/{active.DevId}/set",
                stat_t = $"smartlamp/{active.DevId}/state",
                schema = "json",
                brightness = true,
                color_mode = true,
                supported_color_modes = new[] { "color_temp", "rgb" },
                device = new
                {
                    identifiers = new[] { active.DevId },
                    name = active.Name,
                    model = "SmartLamp Studio V2",
                    manufacturer = "Murr Smart Home"
                }
            };
            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task<string> HandleCommandAsync(string body)
        {
            bool turnOn = body.Contains("\"state\":\"ON\"") || body.Contains("ON") || body.Contains("\"on\":true");
            bool turnOff = body.Contains("\"state\":\"OFF\"") || body.Contains("OFF") || body.Contains("\"on\":false");

            if (turnOn)
            {
                await _protocol.SetPowerAsync(true);
                OnLogMessage?.Invoke("MQTT / HA Command Received: Power ON");
            }
            else if (turnOff)
            {
                await _protocol.SetPowerAsync(false);
                OnLogMessage?.Invoke("MQTT / HA Command Received: Power OFF");
            }

            return JsonSerializer.Serialize(new { result = "success", state = turnOn ? "ON" : "OFF" });
        }
    }
}
