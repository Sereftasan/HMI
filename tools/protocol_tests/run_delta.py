import subprocess, threading, time, sys, socket, struct
sys.argv = ["x", "15022"]
exec(open("modbus_mock.py").read().split('if __name__')[0])

COILS[0x0000]=1; COILS[0x0005]=1; COILS[0x6000+8]=1
REGS[100]=1234; REGS[101]=0xFFFE

ready = threading.Event()
threading.Thread(target=serve, args=(15022, ready), daemon=True).start()
ready.wait()
print("mock listening on 15022", flush=True)
r = subprocess.run(["mono","TDelta.exe","15022"], capture_output=True, text=True, timeout=90)
print(r.stdout)
print(r.stderr[:800])
print("--- requests the driver actually sent ---")
for s in SEEN[:14]: print("  ", s)
sys.exit(r.returncode)

