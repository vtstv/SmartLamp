using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartLampApp.Services
{
    public class GoogleHomeService
    {
        private HttpListener? _listener;
        private bool _isRunning = false;
        private int _port = 8088;
        private readonly TuyaProtocol _protocol;

        public event Action<string>? OnLogMessage;

        public GoogleHomeService(TuyaProtocol protocol)
        {
            _protocol = protocol;
        }

        public bool IsRunning => _isRunning;
        public int Port => _port;

        public void StartServer(int port = 8088)
        {
            if (_isRunning) return;
            _port = port;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/google-smart-home/");
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/google-smart-home/");
                _listener.Start();
                _isRunning = true;

                OnLogMessage?.Invoke($"Google Smart Home Local Bridge listening on port {_port}");
                Task.Run(() => ListenLoop());
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Failed to start Google Home Bridge: {ex.Message}");
                _isRunning = false;
            }
        }

        public void StopServer()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
        }

        private async Task ListenLoop()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
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
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

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

                if (req.Url?.AbsolutePath.Contains("sync") == true || body.Contains("action.devices.intent.SYNC"))
                {
                    responseJson = HandleSyncIntent();
                }
                else if (req.Url?.AbsolutePath.Contains("execute") == true || body.Contains("action.devices.intent.EXECUTE"))
                {
                    responseJson = await HandleExecuteIntentAsync(body);
                }
                else
                {
                    responseJson = JsonSerializer.Serialize(new
                    {
                        status = "online",
                        service = "SmartLamp Studio Google Home Bridge",
                        version = "2.0"
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

        private string HandleSyncIntent()
        {
            var active = _protocol.GetActiveDevice();
            var payload = new
            {
                requestId = Guid.NewGuid().ToString(),
                payload = new
                {
                    agentUserId = "murr_smartlamp_user",
                    devices = new[]
                    {
                        new
                        {
                            id = active.DevId,
                            type = "action.devices.types.LIGHT",
                            traits = new[]
                            {
                                "action.devices.traits.OnOff",
                                "action.devices.traits.Brightness",
                                "action.devices.traits.ColorSetting"
                            },
                            name = new
                            {
                                defaultNames = new[] { "Smart Lamp" },
                                name = active.Name,
                                nicknames = new[] { active.Name, "Lamp", "Light" }
                            },
                            willReportState = true,
                            deviceInfo = new
                            {
                                manufacturer = "Tuya / Murr",
                                model = "SmartLamp-V2",
                                hwVersion = "1.0",
                                swVersion = "2.0"
                            }
                        }
                    }
                }
            };
            return JsonSerializer.Serialize(payload);
        }

        private async Task<string> HandleExecuteIntentAsync(string requestBody)
        {
            bool turnOn = requestBody.Contains("\"on\": true") || requestBody.Contains("turn_on") || requestBody.Contains("ON");
            bool turnOff = requestBody.Contains("\"on\": false") || requestBody.Contains("turn_off") || requestBody.Contains("OFF");

            if (turnOn)
            {
                await _protocol.SetPowerAsync(true);
                OnLogMessage?.Invoke("Google Assistant Executed: Turn ON");
            }
            else if (turnOff)
            {
                await _protocol.SetPowerAsync(false);
                OnLogMessage?.Invoke("Google Assistant Executed: Turn OFF");
            }

            return JsonSerializer.Serialize(new
            {
                requestId = Guid.NewGuid().ToString(),
                payload = new
                {
                    commands = new[]
                    {
                        new
                        {
                            ids = new[] { _protocol.GetActiveDevice().DevId },
                            status = "SUCCESS",
                            states = new
                            {
                                online = true,
                                on = turnOn
                            }
                        }
                    }
                }
            });
        }
    }
}
