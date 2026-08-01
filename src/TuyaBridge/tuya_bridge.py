# ============================================================================
# Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
# Licensed under the MIT License. See LICENSE file in the project root.
# ============================================================================

import sys
import json
import tinytuya
from http.server import HTTPServer, BaseHTTPRequestHandler

devices = {}

def get_device(dev_id, ip, local_key, version):
    key = (dev_id, ip)
    if key not in devices:
        try:
            v = float(version) if version else 3.5
        except:
            v = 3.5
        b = tinytuya.BulbDevice(dev_id, ip, local_key, version=v)
        b.set_socketPersistent(True)
        devices[key] = b
    return devices[key]

cloud_instances = {}

def get_cloud(region, access_id, access_key):
    key = (region, access_id, access_key)
    if key not in cloud_instances:
        cloud_instances[key] = tinytuya.Cloud(region, access_id, access_key)
    return cloud_instances[key]

class BridgeHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == '/health':
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"status": "ok"}).encode('utf-8'))
            return

    def do_POST(self):
        content_length = int(self.headers.get('Content-Length', 0))
        post_data = self.rfile.read(content_length)
        req = json.loads(post_data.decode('utf-8'))

        action = req.get('action')
        dev_id = req.get('dev_id')
        ip = req.get('ip')
        local_key = req.get('local_key')
        version = req.get('version', '3.5')

        if action == 'health':
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"status": "ok"}).encode('utf-8'))
            return

        res = {"success": False}
        try:
            if action == 'cloud_command':
                region = req.get('region', 'eu')
                access_id = req.get('access_id')
                access_key = req.get('access_key')
                c = get_cloud(region, access_id, access_key)

                cmd_type = req.get('cmd_type')
                if cmd_type == 'status':
                    st = c.getstatus(dev_id)
                    if isinstance(st, dict) and st.get('success'):
                        dps = {}
                        for item in st.get('result', []):
                            code = item.get('code')
                            val = item.get('value')
                            if code in ('switch_led', 'switch_1', 'switch'):
                                dps['20'] = val
                            elif code == 'work_mode':
                                dps['21'] = val
                            elif code in ('bright_value_v2', 'bright_value'):
                                dps['22'] = val
                            elif code in ('temp_value_v2', 'temp_value'):
                                dps['23'] = val
                        res["status"] = {"dps": dps}
                        res["success"] = True
                    else:
                        res["error"] = "Cloud status query failed"
                elif cmd_type == 'toggle':
                    st = c.getstatus(dev_id)
                    is_on = False
                    if isinstance(st, dict) and st.get('success'):
                        for item in st.get('result', []):
                            if item.get('code') in ('switch_led', 'switch_1', 'switch'):
                                is_on = bool(item.get('value'))
                                break
                    target = not is_on
                    c_res = c.sendcommand(dev_id, {'commands': [{'code': 'switch_led', 'value': target}]})
                    if isinstance(c_res, dict) and c_res.get('success'):
                        res["success"] = True
                        res["power"] = target
                    else:
                        res["error"] = "Cloud toggle failed"
                else:
                    cmds = []
                    if cmd_type == 'set_power':
                        on = req.get('power', True)
                        cmds.append({'code': 'switch_led', 'value': on})
                    elif cmd_type == 'set_brightness':
                        pct = req.get('brightness', 100)
                        cmds.append({'code': 'switch_led', 'value': True})
                        cmds.append({'code': 'bright_value_v2', 'value': int(pct * 10)})
                    elif cmd_type == 'set_temp':
                        val_v2 = req.get('temp', 1000)
                        cmds.append({'code': 'switch_led', 'value': True})
                        cmds.append({'code': 'work_mode', 'value': 'white'})
                        cmds.append({'code': 'temp_value_v2', 'value': val_v2})
                    elif cmd_type == 'set_preset':
                        mode = req.get('mode', 'white')
                        bright = req.get('brightness', 50)
                        bright_v2 = max(10, min(1000, int(bright * 10)))
                        cmds.append({'code': 'switch_led', 'value': True})
                        if mode == 'colour':
                            cmds.append({'code': 'work_mode', 'value': 'colour'})
                            cmds.append({'code': 'bright_value_v2', 'value': bright_v2})
                        else:
                            val_v2 = req.get('temp', 0)
                            cmds.append({'code': 'work_mode', 'value': 'white'})
                            cmds.append({'code': 'bright_value_v2', 'value': bright_v2})
                            cmds.append({'code': 'temp_value_v2', 'value': val_v2})

                    if cmds:
                        c_res = c.sendcommand(dev_id, {'commands': cmds})
                        if isinstance(c_res, dict) and c_res.get('success'):
                            res["success"] = True
                        else:
                            res["error"] = f"Cloud command failed: {c_res}"
            elif action == 'status':
                b = get_device(dev_id, ip, local_key, version)
                res["status"] = b.status()
                res["success"] = True
            elif action == 'set_power':
                b = get_device(dev_id, ip, local_key, version)
                on = req.get('power', True)
                b.set_status(on, switch=20)
                res["success"] = True
            elif action == 'toggle':
                b = get_device(dev_id, ip, local_key, version)
                st = b.status()
                dps = st.get('dps', {})
                current = dps.get('20', dps.get('1', False))
                target = not bool(current)
                b.set_status(target, switch=20)
                res["power"] = target
                res["success"] = True
            elif action == 'set_brightness':
                b = get_device(dev_id, ip, local_key, version)
                pct = req.get('brightness', 100)
                b.set_multiple_values({20: True, 22: int(pct * 10)})
                res["success"] = True
            elif action == 'set_temp':
                b = get_device(dev_id, ip, local_key, version)
                val_v2 = req.get('temp', 1000)
                b.set_multiple_values({20: True, 21: 'white', 23: val_v2})
                res["success"] = True
            elif action == 'set_color':
                b = get_device(dev_id, ip, local_key, version)
                tuya_hex = req.get('hex', '000003e803e8')
                b.set_multiple_values({20: True, 21: 'colour', 24: tuya_hex})
                res["success"] = True
            elif action == 'set_preset':
                b = get_device(dev_id, ip, local_key, version)
                mode = req.get('mode', 'white')
                bright = req.get('brightness', 50)
                bright_v2 = max(10, min(1000, int(bright * 10)))
                if mode == 'colour':
                    tuya_hex = req.get('hex', '000003e803e8')
                    b.set_multiple_values({20: True, 21: 'colour', 22: bright_v2, 24: tuya_hex})
                else:
                    val_v2 = req.get('temp', 0)
                    b.set_multiple_values({20: True, 21: 'white', 22: bright_v2, 23: val_v2})
                res["success"] = True
            elif action == 'scan':
                res["devices"] = tinytuya.deviceScan(verbose=False)
                res["success"] = True
            elif action == 'cloud_devices':
                region = req.get('region')
                access_id = req.get('access_id')
                access_key = req.get('access_key')
                c = tinytuya.Cloud(region, access_id, access_key)
                res["devices"] = c.getdevices()
                res["success"] = True
        except Exception as e:
            res["error"] = str(e)
            if (dev_id, ip) in devices:
                del devices[(dev_id, ip)]

        self.send_response(200)
        self.send_header('Content-type', 'application/json')
        self.end_headers()
        self.wfile.write(json.dumps(res).encode('utf-8'))

    def log_message(self, format, *args):
        return  # silence console logging

def run_server(port=18889):
    server_address = ('127.0.0.1', port)
    try:
        httpd = HTTPServer(server_address, BridgeHandler)
    except OSError:
        # Address already in use! Exit immediately so duplicate processes never linger!
        sys.exit(0)
    print(f"TuyaBridge Server running on http://127.0.0.1:{port}")
    httpd.serve_forever()

if __name__ == "__main__":
    if len(sys.argv) >= 2 and sys.argv[1] == "--server":
        port = int(sys.argv[2]) if len(sys.argv) >= 3 else 18889
        run_server(port)
    elif len(sys.argv) >= 3 and sys.argv[1] == "-c":
        exec_globals = {'tinytuya': tinytuya, 'json': json}
        try:
            code = sys.argv[2]
            code = code.replace("import tinytuya, json;", "").strip()
            code = code.replace("import tinytuya;", "").strip()
            exec(code, exec_globals)
        except Exception as e:
            print("Error:", e)
    else:
        print("Usage: tuya_bridge.exe --server [port] OR -c \"python code\"")
