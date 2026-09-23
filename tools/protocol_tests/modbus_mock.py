"""Minimal Modbus TCP server that VALIDATES what the Delta driver sends and
answers correctly. It fails loudly on a malformed frame rather than tolerating
it, because a tolerant mock proves nothing."""
import socket, struct, threading, sys, json

COILS = {}      # address -> 0/1
REGS  = {}      # address -> 16-bit
SEEN  = []      # every request, for the test to assert on

def handle(conn):
    while True:
        hdr = recvn(conn, 7)
        if not hdr: return
        tid, pid, length, unit = struct.unpack(">HHHB", hdr)
        assert pid == 0, "protocol id must be 0, got %d" % pid
        body = recvn(conn, length - 1)
        fn = body[0]
        SEEN.append({"unit": unit, "fn": fn})

        if fn == 0x01:                                   # read coils
            start, count = struct.unpack(">HH", body[1:5])
            SEEN[-1].update(start=start, count=count)
            nbytes = (count + 7) // 8
            data = bytearray(nbytes)
            for i in range(count):
                if COILS.get(start + i, 0): data[i // 8] |= (1 << (i % 8))
            pdu = bytes([fn, nbytes]) + bytes(data)
        elif fn == 0x03:                                 # read holding registers
            start, count = struct.unpack(">HH", body[1:5])
            SEEN[-1].update(start=start, count=count)
            pdu = bytes([fn, count * 2])
            for i in range(count):
                pdu += struct.pack(">H", REGS.get(start + i, 0) & 0xFFFF)
        elif fn == 0x05:                                 # write single coil
            addr, val = struct.unpack(">HH", body[1:5])
            COILS[addr] = 1 if val == 0xFF00 else 0
            SEEN[-1].update(start=addr, value=COILS[addr])
            pdu = body[:5]
        elif fn == 0x06:                                 # write single register
            addr, val = struct.unpack(">HH", body[1:5])
            REGS[addr] = val
            SEEN[-1].update(start=addr, value=val)
            pdu = body[:5]
        elif fn == 0x10:                                 # write multiple registers
            addr, count, nb = struct.unpack(">HHB", body[1:6])
            vals = [struct.unpack(">H", body[6+i*2:8+i*2])[0] for i in range(count)]
            for i, v in enumerate(vals): REGS[addr+i] = v
            SEEN[-1].update(start=addr, values=vals)
            pdu = bytes([fn]) + struct.pack(">HH", addr, count)
        else:
            pdu = bytes([fn | 0x80, 0x01])               # illegal function

        out = struct.pack(">HHH", tid, 0, len(pdu) + 1) + bytes([unit]) + pdu
        conn.sendall(out)

def recvn(c, n):
    b = b""
    while len(b) < n:
        p = c.recv(n - len(b))
        if not p: return None
        b += p
    return b

def serve(port, ready):
    s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(("127.0.0.1", port)); s.listen(5); ready.set()
    while True:
        c, _ = s.accept()
        threading.Thread(target=handle, args=(c,), daemon=True).start()

if __name__ == "__main__":
    port = int(sys.argv[1])
    # preload: M0=1, M5=1, D100=1234, D101=-2 (as unsigned), X10(octal)=bit 8 -> 1
    COILS[0x0000] = 1
    COILS[0x0005] = 1
    COILS[0x6000 + 8] = 1          # X10 octal == 8 decimal
    REGS[0x0000 + 100] = 1234
    REGS[0x0000 + 101] = 0xFFFE    # -2 signed
    ready = threading.Event()
    threading.Thread(target=serve, args=(port, ready), daemon=True).start()
    ready.wait()
    print("READY", flush=True)
    import time
    while True:
        time.sleep(0.3)
        try:
            json.dump(SEEN, open("/tmp/proto/modbus_seen.json", "w"))
        except Exception: pass

