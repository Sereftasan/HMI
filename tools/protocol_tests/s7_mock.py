"""Minimal S7 (ISO-on-TCP) server that validates the frames the Siemens driver
sends: COTP connection request, S7 setup communication, read var and write var.
Asserts hard on anything malformed."""
import socket, struct, threading

MEM = {0x83: bytearray(256), 0x81: bytearray(256), 0x82: bytearray(256)}   # M, I, Q
DBS = {10: bytearray(256)}
SEEN = []

def recvn(c, n):
    b = b""
    while len(b) < n:
        p = c.recv(n - len(b))
        if not p: return None
        b += p
    return b

def send_tpkt(c, payload):
    c.sendall(struct.pack(">BBH", 3, 0, len(payload) + 4) + payload)

def area_buf(area, db):
    return DBS.setdefault(db, bytearray(256)) if area == 0x84 else MEM[area]

def handle(c):
    while True:
        hdr = recvn(c, 4)
        if not hdr: return
        assert hdr[0] == 3, "TPKT version must be 3"
        total = struct.unpack(">H", hdr[2:4])[0]
        body = recvn(c, total - 4)

        if body[1] == 0xE0:                       # COTP connection request
            dst = None
            i = 7
            while i < len(body):                   # walk the parameters
                code, ln = body[i], body[i+1]
                if code == 0xC2: dst = struct.unpack(">H", body[i+2:i+4])[0]
                i += 2 + ln
            SEEN.append({"cotp": "CR", "dst_tsap": hex(dst)})
            # connection confirm, echoing the TSAPs
            send_tpkt(c, bytes([0x11, 0xD0, 0x00, 0x01, 0x00, 0x01, 0x00,
                                0xC1, 0x02, 0x01, 0x00,
                                0xC2, 0x02, (dst >> 8) & 0xFF, dst & 0xFF,
                                0xC0, 0x01, 0x0A]))
            continue

        assert body[:3] == b"\x02\xf0\x80", "expected COTP DT"
        assert body[3] == 0x32, "expected S7 protocol id 0x32"
        assert body[4] == 0x01, "expected job request"
        plen = struct.unpack(">H", body[9:11])[0]
        dlen = struct.unpack(">H", body[11:13])[0]
        params = body[13:13+plen]
        data = body[13+plen:13+plen+dlen]
        pduref = body[7:9]
        fn = params[0]

        if fn == 0xF0:                             # setup communication
            SEEN.append({"s7": "setup"})
            rp = bytes([0xF0, 0x00, 0x00, 0x03, 0x00, 0x03, 0x03, 0xC0])
            reply(c, pduref, rp, b"")
            continue

        items = []
        for k in range(params[1]):
            it = params[2 + k*12 : 14 + k*12]
            assert it[0] == 0x12 and it[1] == 0x0A and it[2] == 0x10, "bad item spec"
            tsize = it[3]
            count = struct.unpack(">H", it[4:6])[0]
            db    = struct.unpack(">H", it[6:8])[0]
            area  = it[8]
            start = (it[9] << 16) | (it[10] << 8) | it[11]
            items.append((tsize, count, db, area, start))

        if fn == 0x04:                             # read var
            SEEN.append({"s7": "read", "items": items})
            out = b""
            for tsize, count, db, area, startbits in items:
                buf = area_buf(area, db)
                if tsize == 0x01:                  # BIT
                    byte, bit = startbits // 8, startbits % 8
                    val = (buf[byte] >> bit) & 1
                    out += bytes([0xFF, 0x03, 0x00, 0x01, val])
                    out += b"\x00"                 # pad to even
                else:                              # BYTE/WORD/DWORD
                    byte = startbits // 8
                    payload = bytes(buf[byte:byte+count])
                    out += bytes([0xFF, 0x04]) + struct.pack(">H", count * 8) + payload
                    if len(payload) % 2: out += b"\x00"
            reply(c, pduref, bytes([0x04, len(items)]), out)

        elif fn == 0x05:                           # write var
            SEEN.append({"s7": "write", "items": items})
            at = 0
            for tsize, count, db, area, startbits in items:
                rc, transport = data[at], data[at+1]
                blen = struct.unpack(">H", data[at+2:at+4])[0]
                nbytes = (blen + 7) // 8 if transport in (0x03, 0x04) else blen
                payload = data[at+4:at+4+nbytes]
                buf = area_buf(area, db)
                if tsize == 0x01:
                    byte, bit = startbits // 8, startbits % 8
                    if payload[0] & 1: buf[byte] |= (1 << bit)
                    else:              buf[byte] &= ~(1 << bit) & 0xFF
                else:
                    byte = startbits // 8
                    buf[byte:byte+len(payload)] = payload
                at += 4 + nbytes
                if nbytes % 2: at += 1
            reply(c, pduref, bytes([0x05, len(items)]), bytes([0xFF] * len(items)))

def reply(c, pduref, params, data):
    h = bytes([0x02, 0xF0, 0x80, 0x32, 0x03, 0x00, 0x00]) + pduref
    h += struct.pack(">HH", len(params), len(data)) + bytes([0x00, 0x00])   # err class/code
    send_tpkt(c, h + params + data)

def serve(port, ready):
    s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(("127.0.0.1", port)); s.listen(5); ready.set()
    while True:
        conn, _ = s.accept()
        threading.Thread(target=handle, args=(conn,), daemon=True).start()

