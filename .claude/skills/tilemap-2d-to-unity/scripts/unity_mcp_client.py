#!/usr/bin/env python3
"""Drive the OPEN Unity Editor through the running MCP-for-Unity server (HTTP, 127.0.0.1:8082/mcp).

Batch mode needs Unity closed; this works while the Editor is open (its MCP plugin must be connected).

    python _tools/unity_mcp_client.py list                     # tools
    python _tools/unity_mcp_client.py exec "return 1+1;"        # C# method body (execute_code)
    python _tools/unity_mcp_client.py exec path/to/code.cs
    python _tools/unity_mcp_client.py call refresh_unity '{"mode":"force","compile":"request","wait_for_ready":true}'
    python _tools/unity_mcp_client.py wait                     # until the Editor answers and is idle
    python _tools/unity_mcp_client.py refresh                  # refresh assets + compile, then wait

Notes: a call that blocks the Editor for more than ~25 s drops the plugin connection (the work
still finishes); keep each call short (one map per call) and use `wait` after heavy imports.
EditorApplication.delayCall does not fire while the Editor sits in the background.
"""
import json
import sys
import time
import urllib.request

URL = "http://127.0.0.1:8082/mcp"
HEADERS = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}


def post(body, sid=None, timeout=900):
    headers = dict(HEADERS)
    if sid:
        headers["mcp-session-id"] = sid
    req = urllib.request.Request(URL, json.dumps(body).encode(), headers)
    with urllib.request.urlopen(req, timeout=timeout) as r:
        sid = r.headers.get("mcp-session-id") or sid
        raw = r.read().decode("utf-8", "replace")
    out = None
    for line in raw.splitlines():
        if line.startswith("data:"):
            out = json.loads(line[5:])
    if out is None and raw.strip().startswith("{"):
        out = json.loads(raw)
    return sid, out


def session():
    sid, _ = post({"jsonrpc": "2.0", "id": 1, "method": "initialize",
                   "params": {"protocolVersion": "2025-03-26", "capabilities": {},
                              "clientInfo": {"name": "boom-cli", "version": "1"}}})
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    return sid


def call(sid, name, args):
    _, r = post({"jsonrpc": "2.0", "id": 2, "method": "tools/call", "params": {"name": name, "arguments": args}}, sid)
    res = (r or {}).get("result", r) or {}
    return "\n".join(c.get("text", str(c)) for c in res.get("content", [])) or json.dumps(res)


def execute(sid, code):
    return call(sid, "execute_code", {"action": "execute", "code": code, "safety_checks": False})


def main():
    cmd = sys.argv[1]
    sid = session()
    if cmd == "list":
        _, r = post({"jsonrpc": "2.0", "id": 2, "method": "tools/list"}, sid)
        for t in r["result"]["tools"]:
            print(t["name"], "-", (t.get("description") or "")[:100].replace("\n", " "))
    elif cmd == "exec":
        arg = sys.argv[2]
        code = open(arg, encoding="utf-8").read() if arg.endswith(".cs") else arg
        print(execute(sid, code))
    elif cmd == "refresh":
        # Asset refresh + script compile, then wait until the Editor is idle again.
        print(call(sid, "refresh_unity", {"mode": "force", "compile": "request", "wait_for_ready": True})[:200])
        time.sleep(3)
        sys.argv[1:] = ["wait"]
        main()
    elif cmd == "console":
        # Last errors/warnings of the Editor console (JSON args get mangled by Git Bash).
        print(call(sid, "read_console", {"types": ["error", "warning"], "count": int(sys.argv[2]) if len(sys.argv) > 2 else 20}))
    elif cmd == "wait":
        for _ in range(120):
            try:
                out = execute(session(), 'return "idle " + (EditorApplication.isCompiling || EditorApplication.isUpdating);')
                if '"idle False"' in out:
                    print("ready")
                    return
            except Exception:
                pass
            time.sleep(5)
        print("timeout")
    else:
        print(call(sid, cmd, json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}))


if __name__ == "__main__":
    main()
