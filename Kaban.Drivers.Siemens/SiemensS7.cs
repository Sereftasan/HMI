using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Kaban.Core.Interfaces;

namespace Kaban.Drivers.Siemens
{
    /// <summary>
    /// SIEMENS S7 driver over ISO-on-TCP (RFC1006, port 102) - S7-1200/1500 and
    /// S7-300/400. The S7-1200 is the part that sits where a Mitsubishi FX5 sits.
    ///
    /// WHY A RAW SOCKET AND NOT A VENDOR SDK
    /// The same reason as the Delta driver: no Windows install, no COM object, no
    /// bitness trap. MX Component's 32-bit-only registration is already the single
    /// most expensive constraint in this program (see the handoff), and adding a
    /// second SDK with its own version of that problem would be a poor trade. The
    /// S7 protocol over TCP needs nothing on the PC but an open port.
    ///
    /// NOT VERIFIED AGAINST HARDWARE. Written from the published S7comm frame
    /// layout; nobody here has an S7 CPU to test against. Expect the first
    /// commissioning to be a bring-up rather than a plug-in.
    ///
    /// THREE THINGS TO SET ON THE CPU or nothing will connect, none of which this
    /// driver can do for you:
    ///   1. In the DB properties, clear "Optimized block access". An optimised DB
    ///      has no byte addresses to read, so DB reads fail no matter what is sent.
    ///   2. Under Protection, tick "Permit access with PUT/GET communication from
    ///      remote partner". It is OFF by default on the 1200/1500 and is the most
    ///      common reason a correct-looking connection is refused.
    ///   3. Give the CPU an IP the panel PC can reach.
    ///
    /// THE ENDPOINT STRING (from Settings -> HMI Settings -> Station Connection)
    ///     192.168.0.1             rack 0, slot 1  (the S7-1200/1500 default)
    ///     192.168.0.1:0:1         explicit rack and slot
    ///     192.168.0.1:0:2         S7-300/400 usually want slot 2
    ///
    /// ADDRESSES are written the way the Siemens engineer writes them:
    ///     M10.3     bit 3 of memory byte 10
    ///     MW20      memory word 20            MD20  memory double word
    ///     I0.5      input bit                 Q0.1  output bit
    ///     DB10.DBX4.2   bit                   DB10.DBW4 / DB10.DBD4   word / dword
    /// </summary>
    public class SiemensS7 : IPlcDriver, IDisposable
    {
        private readonly string _host;
        private readonly int _rack;
        private readonly int _slot;

        private TcpClient _client;
        private NetworkStream _stream;
        private readonly object _gate = new object();

        private bool _lastOpOk = true;
        private ushort _pduReference;
        private int _negotiatedPdu = 240;
        private int _failCount;
        private DateTime _lastFailLog = DateTime.MinValue;

        /// <summary>Last connection failure, for PLC Diagnostics.</summary>
        public string LastConnectError { get; private set; }

        public SiemensS7(string endpoint)
        {
            var parts = (endpoint ?? "").Split(':');
            _host = parts.Length > 0 && parts[0].Trim().Length > 0 ? parts[0].Trim() : "192.168.0.1";

            int r, s;
            _rack = parts.Length > 1 && int.TryParse(parts[1].Trim(), out r) ? r : 0;
            _slot = parts.Length > 2 && int.TryParse(parts[2].Trim(), out s) ? s : 1;
        }

        public string Endpoint { get { return _host + ":" + _rack + ":" + _slot; } }

        // ==================================================================
        // CONNECTION
        // ==================================================================

        public bool IsConnected
        {
            get { lock (_gate) return _client != null && _client.Connected; }
        }

        public bool IsHealthy { get { return IsConnected && _lastOpOk; } }

        /// <summary>
        /// Three steps, all of which must succeed: TCP, then the ISO-TSAP
        /// connection request that carries the rack/slot, then the S7 setup that
        /// agrees a PDU size. A CPU that refuses at step 2 is nearly always
        /// answering the wrong slot.
        /// </summary>
        public bool Connect()
        {
            lock (_gate)
            {
                Close();
                try
                {
                    _client = new TcpClient();
                    var async = _client.BeginConnect(_host, 102, null, null);
                    if (!async.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(1500)))
                    {
                        Close();
                        LastConnectError = "timed out opening " + _host + ":102";
                        LogFailure();
                        return false;
                    }
                    _client.EndConnect(async);
                    _client.NoDelay = true;
                    _client.ReceiveTimeout = 1500;
                    _client.SendTimeout = 1500;
                    _stream = _client.GetStream();

                    if (!IsoConnectionRequest()) { Close(); LogFailure(); return false; }
                    if (!NegotiatePdu()) { Close(); LogFailure(); return false; }

                    _lastOpOk = true;
                    _failCount = 0;
                    LastConnectError = "";
                    Kaban.Core.Helpers.AuditLogger.Log(
                        "[PLC DIAG] Siemens S7 connected to " + Endpoint + ", PDU " + _negotiatedPdu + ".");
                    return true;
                }
                catch (Exception ex)
                {
                    Close();
                    LastConnectError = ex.Message;
                    LogFailure();
                    return false;
                }
            }
        }

        private void LogFailure()
        {
            if (_failCount++ != 0 && (DateTime.Now - _lastFailLog).TotalSeconds < 60) return;
            _lastFailLog = DateTime.Now;
            Kaban.Core.Helpers.AuditLogger.Log(
                "[PLC DIAG] Siemens S7 " + Endpoint + " connect failed: " + LastConnectError +
                ". Check that PUT/GET communication is permitted and the slot is right" +
                (_failCount > 1 ? " [repeated " + _failCount + " times]" : "") + ".");
        }

        /// <summary>
        /// COTP connection request. The destination TSAP encodes rack and slot as
        /// 0x0100 | (rack * 32 + slot) - this is the byte the CPU checks, and a
        /// wrong slot is refused here rather than later.
        /// </summary>
        private bool IsoConnectionRequest()
        {
            int dstTsap = 0x0100 | ((_rack * 0x20) + _slot);

            var cotp = new byte[]
            {
                0x11,                   // length indicator
                0xE0,                   // CR - connection request
                0x00, 0x00,             // destination reference
                0x00, 0x01,             // source reference
                0x00,                   // class 0
                0xC1, 0x02, 0x01, 0x00, // source TSAP
                0xC2, 0x02,             // destination TSAP
                (byte)((dstTsap >> 8) & 0xFF), (byte)(dstTsap & 0xFF),
                0xC0, 0x01, 0x0A        // TPDU size 1024
            };

            SendTpkt(cotp);
            var reply = ReceiveTpkt();
            // 0xD0 = connection confirm; anything else means the CPU said no
            if (reply != null && reply.Length > 1 && reply[1] == 0xD0) return true;

            LastConnectError = "ISO connection refused (rack " + _rack + ", slot " + _slot + ")";
            return false;
        }

        /// <summary>Agrees the maximum PDU length; every later read is capped by it.</summary>
        private bool NegotiatePdu()
        {
            var setup = new byte[]
            {
                0x02, 0xF0, 0x80,             // COTP data
                0x32,                         // S7 protocol id
                0x01,                         // job request
                0x00, 0x00,                   // redundancy id
                0x00, 0x00,                   // pdu reference
                0x00, 0x08,                   // parameter length
                0x00, 0x00,                   // data length
                0xF0,                         // setup communication
                0x00,
                0x00, 0x03,                   // max AMQ caller
                0x00, 0x03,                   // max AMQ callee
                0x03, 0xC0                    // requested PDU length 960
            };

            SendTpkt(setup);
            var reply = ReceiveTpkt();

            // ack_data layout again: COTP 0..2, S7 header 3..14, then the setup
            // parameter block - 15 function, 16 reserved, 17..18 max AMQ caller,
            // 19..20 max AMQ callee, 21..22 the agreed PDU length.
            if (reply == null || reply.Length < 23)
            {
                LastConnectError = "S7 setup communication failed";
                return false;
            }
            _negotiatedPdu = (reply[21] << 8) | reply[22];
            if (_negotiatedPdu < 240 || _negotiatedPdu > 960) _negotiatedPdu = 240;
            return true;
        }

        public void Disconnect() { lock (_gate) Close(); }

        private void Close()
        {
            try { if (_stream != null) _stream.Dispose(); } catch { }
            try { if (_client != null) _client.Close(); } catch { }
            _stream = null;
            _client = null;
        }

        public void Dispose() { Disconnect(); }

        // ==================================================================
        // READ / WRITE
        // ==================================================================

        public int Read(string address)
        {
            int value;
            TryRead(address, out value);
            return value;
        }

        public bool TryRead(string address, out int value)
        {
            int[] values;
            bool ok = TryReadMulti(new[] { address }, out values);
            value = values.Length > 0 ? values[0] : 0;
            return ok;
        }

        public int[] ReadMulti(string[] addresses)
        {
            int[] values;
            TryReadMulti(addresses, out values);
            return values;
        }

        /// <summary>
        /// Batch read. S7 lets several items ride in ONE request, which is the whole
        /// reason the scanner batches: a screenful of devices costs one round trip
        /// instead of thirty. The batch is split to stay inside the negotiated PDU.
        /// </summary>
        public bool TryReadMulti(string[] addresses, out int[] values)
        {
            values = new int[addresses == null ? 0 : addresses.Length];
            if (addresses == null || addresses.Length == 0) return true;

            var parsed = addresses.Select(S7Address.Parse).ToArray();
            bool allOk = parsed.All(p => p != null);

            lock (_gate)
            {
                if (!IsConnected && !Connect()) return false;

                // 12 bytes of request per item, and roughly 4 + size bytes back
                int perRequest = Math.Max(1, Math.Min(16, (_negotiatedPdu - 32) / 16));

                var wanted = Enumerable.Range(0, parsed.Length).Where(i => parsed[i] != null).ToList();

                for (int offset = 0; offset < wanted.Count; offset += perRequest)
                {
                    var slice = wanted.Skip(offset).Take(perRequest).ToList();
                    try
                    {
                        var results = ReadItems(slice.Select(i => parsed[i]).ToList());
                        if (results == null) { allOk = false; continue; }

                        for (int k = 0; k < slice.Count; k++)
                        {
                            var bytes = results[k];
                            if (bytes == null) { allOk = false; continue; }
                            values[slice[k]] = parsed[slice[k]].Decode(bytes);
                        }
                    }
                    catch (Exception ex)
                    {
                        allOk = false;
                        Close();
                        Kaban.Core.Helpers.AuditLogger.Log("Siemens read failed: " + ex.Message);
                        break;
                    }
                }

                _lastOpOk = allOk;
                return allOk;
            }
        }

        /// <summary>One S7 read-variable job. Returns the raw bytes per item.</summary>
        private List<byte[]> ReadItems(List<S7Address> items)
        {
            var parameters = new List<byte> { 0x04, (byte)items.Count };
            foreach (var item in items) parameters.AddRange(item.ToRequestItem());

            var reply = S7Job(parameters.ToArray(), new byte[0]);
            if (reply == null) return null;

            // reply: [0..1] param len, [2..3] data len, then the parameter block,
            // then one data block per item
            int paramLength = (reply[0] << 8) | reply[1];
            int at = 4 + paramLength;    // skip the echoed function/count

            var results = new List<byte[]>();
            for (int i = 0; i < items.Count; i++)
            {
                if (at + 4 > reply.Length) { results.Add(null); continue; }

                byte returnCode = reply[at];
                byte transport = reply[at + 1];
                int length = (reply[at + 2] << 8) | reply[at + 3];
                if (transport == 0x03 || transport == 0x04) length = (length + 7) / 8;   // bit/byte counts are in bits
                at += 4;

                if (returnCode != 0xFF || at + length > reply.Length)
                {
                    // 0x0A = "object does not exist", the usual answer for an
                    // optimised DB or a wrong DB number. Not a value of zero.
                    results.Add(null);
                    at += length;
                }
                else
                {
                    var bytes = new byte[length];
                    Array.Copy(reply, at, bytes, 0, length);
                    results.Add(bytes);
                    at += length;
                }
                if (length % 2 != 0) at++;    // items are padded to an even length
            }
            return results;
        }

        public int Write(string address, int value)
        {
            var item = S7Address.Parse(address);
            if (item == null) { _lastOpOk = false; return -1; }

            lock (_gate)
            {
                if (!IsConnected && !Connect()) return -1;

                try
                {
                    var payload = item.Encode(value);

                    var parameters = new List<byte> { 0x05, 0x01 };
                    parameters.AddRange(item.ToRequestItem());

                    var data = new List<byte>
                    {
                        0x00,                                       // reserved
                        item.IsBit ? (byte)0x03 : (byte)0x04,       // bit vs byte transport
                    };
                    int bitLength = item.IsBit ? 1 : payload.Length * 8;
                    data.Add((byte)((bitLength >> 8) & 0xFF));
                    data.Add((byte)(bitLength & 0xFF));
                    data.AddRange(payload);
                    if (data.Count % 2 != 0) data.Add(0x00);

                    var reply = S7Job(parameters.ToArray(), data.ToArray());
                    if (reply == null) { _lastOpOk = false; return -1; }

                    int paramLength = (reply[0] << 8) | reply[1];
                    int at = 4 + paramLength;
                    bool ok = at < reply.Length && reply[at] == 0xFF;

                    _lastOpOk = ok;
                    return ok ? 0 : -1;
                }
                catch (Exception ex)
                {
                    _lastOpOk = false;
                    Close();
                    Kaban.Core.Helpers.AuditLogger.Log("Siemens write " + address + " failed: " + ex.Message);
                    return -1;
                }
            }
        }

        // ==================================================================
        // S7 / COTP / TPKT FRAMING
        // ==================================================================

        /// <summary>Sends an S7 job and returns everything after the S7 header.</summary>
        private byte[] S7Job(byte[] parameters, byte[] data)
        {
            unchecked { _pduReference++; }

            var frame = new List<byte>
            {
                0x02, 0xF0, 0x80,                                   // COTP data, last unit
                0x32, 0x01,                                         // S7, job request
                0x00, 0x00,                                         // redundancy id
                (byte)((_pduReference >> 8) & 0xFF), (byte)(_pduReference & 0xFF),
                (byte)((parameters.Length >> 8) & 0xFF), (byte)(parameters.Length & 0xFF),
                (byte)((data.Length >> 8) & 0xFF), (byte)(data.Length & 0xFF)
            };
            frame.AddRange(parameters);
            frame.AddRange(data);

            SendTpkt(frame.ToArray());
            var reply = ReceiveTpkt();

            // reply still carries the 3-byte COTP header, so the S7 header sits at
            // offset 3. An ack_data (0x03) has two error bytes that a job does not,
            // which is why the payload starts at 15 and not 13.
            //
            //   3        0x32 protocol id      9..10   parameter length
            //   4        0x03 ack_data         11..12  data length
            //   5..6     redundancy id         13      error class
            //   7..8     pdu reference         14      error code
            //   15..     parameters, then data
            if (reply == null || reply.Length < 15) return null;
            if (reply[3] != 0x32 || reply[4] != 0x03) return null;

            byte errorClass = reply[13], errorCode = reply[14];
            if (errorClass != 0)
            {
                Kaban.Core.Helpers.AuditLogger.Log(
                    "Siemens S7 error class 0x" + errorClass.ToString("X2") +
                    " code 0x" + errorCode.ToString("X2") + " on " + Endpoint + ".");
                return null;
            }

            // Hand back a flat block the callers can index consistently:
            //   [0..1] parameter length, [2..3] data length, [4..] parameters + data
            var outp = new byte[4 + (reply.Length - 15)];
            outp[0] = reply[9];  outp[1] = reply[10];
            outp[2] = reply[11]; outp[3] = reply[12];
            Array.Copy(reply, 15, outp, 4, reply.Length - 15);
            return outp;
        }

        private void SendTpkt(byte[] payload)
        {
            if (_stream == null) throw new InvalidOperationException("not connected");
            int total = payload.Length + 4;
            var frame = new byte[total];
            frame[0] = 0x03; frame[1] = 0x00;                       // TPKT version
            frame[2] = (byte)((total >> 8) & 0xFF);
            frame[3] = (byte)(total & 0xFF);
            Array.Copy(payload, 0, frame, 4, payload.Length);
            _stream.Write(frame, 0, frame.Length);
            _stream.Flush();
        }

        private byte[] ReceiveTpkt()
        {
            var header = ReadExactly(4);
            if (header[0] != 0x03) throw new InvalidOperationException("not a TPKT frame");
            int total = (header[2] << 8) | header[3];
            if (total < 5 || total > 2048) throw new InvalidOperationException("bad TPKT length " + total);
            return ReadExactly(total - 4);
        }

        private byte[] ReadExactly(int count)
        {
            var buffer = new byte[count];
            int got = 0;
            while (got < count)
            {
                int n = _stream.Read(buffer, got, count - got);
                if (n <= 0) throw new InvalidOperationException("connection closed mid-frame");
                got += n;
            }
            return buffer;
        }
    }

    /// <summary>
    /// Siemens device name -> S7 area, byte offset, bit and width.
    /// Accepts M10.3, MW20, MD20, I0.5, Q0.1, DB10.DBX4.2, DB10.DBW4, DB10.DBD4.
    /// </summary>
    internal class S7Address
    {
        public byte Area;          // 0x83 M, 0x81 I, 0x82 Q, 0x84 DB
        public int DbNumber;
        public int ByteOffset;
        public int Bit = -1;
        public int Width = 1;      // bytes: 1 bit/byte, 2 word, 4 dword

        public bool IsBit { get { return Bit >= 0; } }

        private static readonly Regex DbPattern = new Regex(
            @"^\s*DB(\d+)\s*\.\s*DB(X|B|W|D)\s*(\d+)(?:\s*\.\s*(\d+))?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex FlagPattern = new Regex(
            @"^\s*(MD|MW|MB|M|ID|IW|IB|I|E|QD|QW|QB|Q|A)\s*(\d+)(?:\s*\.\s*(\d+))?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static S7Address Parse(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            string text = address.Trim();

            var db = DbPattern.Match(text);
            if (db.Success)
            {
                var a = new S7Address
                {
                    Area = 0x84,
                    DbNumber = int.Parse(db.Groups[1].Value, CultureInfo.InvariantCulture),
                    ByteOffset = int.Parse(db.Groups[3].Value, CultureInfo.InvariantCulture)
                };
                switch (db.Groups[2].Value.ToUpperInvariant())
                {
                    case "X": a.Width = 1; a.Bit = db.Groups[4].Success
                                  ? int.Parse(db.Groups[4].Value, CultureInfo.InvariantCulture) : 0; break;
                    case "B": a.Width = 1; break;
                    case "W": a.Width = 2; break;
                    case "D": a.Width = 4; break;
                }
                return a.Bit > 7 ? null : a;
            }

            var flag = FlagPattern.Match(text);
            if (!flag.Success) return null;

            string prefix = flag.Groups[1].Value.ToUpperInvariant();
            var result = new S7Address
            {
                ByteOffset = int.Parse(flag.Groups[2].Value, CultureInfo.InvariantCulture)
            };

            // German mnemonics are accepted too: E = Eingang (input), A = Ausgang
            // (output). Engineering drawings on this equipment use both.
            if (prefix.StartsWith("M")) result.Area = 0x83;
            else if (prefix.StartsWith("I") || prefix == "E") result.Area = 0x81;
            else result.Area = 0x82;

            if (prefix.EndsWith("D")) result.Width = 4;
            else if (prefix.EndsWith("W")) result.Width = 2;
            else if (prefix.EndsWith("B")) result.Width = 1;
            else
            {
                // bare M/I/Q is a BIT and needs the .n - M10 alone is ambiguous and
                // silently reading byte 10 instead would be worse than refusing
                if (!flag.Groups[3].Success) return null;
                result.Width = 1;
                result.Bit = int.Parse(flag.Groups[3].Value, CultureInfo.InvariantCulture);
                if (result.Bit > 7) return null;
            }

            return result;
        }

        /// <summary>The 12-byte item descriptor an S7 read/write job carries.</summary>
        public byte[] ToRequestItem()
        {
            int count = IsBit ? 1 : Width;
            int startBits = (ByteOffset * 8) + (IsBit ? Bit : 0);

            return new byte[]
            {
                0x12,                                   // variable specification
                0x0A,                                   // length of the rest
                0x10,                                   // addressing mode: S7ANY
                IsBit ? (byte)0x01 : (byte)0x02,        // transport size: BIT / BYTE
                (byte)((count >> 8) & 0xFF), (byte)(count & 0xFF),
                (byte)((DbNumber >> 8) & 0xFF), (byte)(DbNumber & 0xFF),
                Area,
                (byte)((startBits >> 16) & 0xFF),
                (byte)((startBits >> 8) & 0xFF),
                (byte)(startBits & 0xFF)
            };
        }

        /// <summary>Raw bytes from the CPU -> the signed integer the HMI works in.</summary>
        public int Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return 0;
            if (IsBit) return (bytes[0] & 1) != 0 ? 1 : 0;

            // S7 is BIG-ENDIAN throughout - the opposite of the Mitsubishi and Delta
            // word order, and the easiest thing in this file to get backwards.
            switch (Width)
            {
                case 2: return bytes.Length >= 2 ? (short)((bytes[0] << 8) | bytes[1]) : (sbyte)bytes[0];
                case 4:
                    if (bytes.Length < 4) return 0;
                    return unchecked((int)(((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) |
                                           ((uint)bytes[2] << 8) | bytes[3]));
                default: return (sbyte)bytes[0];
            }
        }

        public byte[] Encode(int value)
        {
            if (IsBit) return new[] { (byte)(value != 0 ? 1 : 0) };
            switch (Width)
            {
                case 2: return new[] { (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF) };
                case 4: return new[]
                    {
                        (byte)((value >> 24) & 0xFF), (byte)((value >> 16) & 0xFF),
                        (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF)
                    };
                default: return new[] { (byte)(value & 0xFF) };
            }
        }
    }
}

