import subprocess, threading, sys
exec(open("s7_mock.py").read())
ready = threading.Event()
# the driver hardcodes port 102, as real S7 does; we can bind it as root here
threading.Thread(target=serve, args=(102, ready), daemon=True).start()
ready.wait()
print("S7 mock listening on 102", flush=True)
r = subprocess.run(["mono","TSiemens.exe"], capture_output=True, text=True, timeout=90)
print(r.stdout); print(r.stderr[:1500])
print("--- first frames the driver sent ---")
for s in SEEN[:6]: print("  ", str(s)[:150])
sys.exit(r.returncode)

