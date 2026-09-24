using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Kaban.Core.Interfaces;

namespace Kaban.Drivers.Delta
{
    /// <summary>
    /// DELTA PLC driver over MODBUS TCP - for the AS/DVP families, the compact
    /// range this company would put where a Mitsubishi FX5 goes today.
    ///
    /// WHY MODBUS TCP AND NOT A VENDOR SDK
    /// Delta's own DIALink/COMMGR stack is a Windows install with its own bitness
    /// problems - exactly the class of trouble MX Component already causes here
    /// (see the 32-bit note in the handoff). Every AS-series and Ethernet DVP CPU
    /// speaks Modbus TCP out of the box with nothing installed on the PC, so this
    /// driver is a plain socket and has no dependency to get wrong. It also means
    /// the HMI can be pointed at ANY Modbus TCP device, not only a Delta.
    ///
    /// NOT VERIFIED AGAINST HARDWARE. It is written from the Modbus specification
    /// and Delta's published device-to-Modbus address table; nobody here has a
    /// Delta CPU to test it on. Treat the first commissioning as a bring-up:
    /// PLC Diagnostics will tell you whether the socket opens, and a single bit
    /// read through the monitor will tell you whether the address mapping matches
    /// your CPU. The address table below is the part most likely to need adjusting
    /// between families.
    ///
    /// ADDRESSES are written the way the Delta engineer writes them - M0, D100,
    /// X0, Y0, S20, T5, C10 - and are translated here to Modbus register/coil
    /// numbers. The HMI never sees a Modbus number.
    ///
    /// THE ENDPOINT STRING (from Settings -> HMI Settings -> Station Connection)
    ///     192.168.1.5             port 502, unit id 1
    ///     192.168.1.5:502         unit id 1
    ///     192.168.1.5:502:2       explicit unit id
    /// </summary>
    public class DeltaModbusTcp : IPlcDriver, IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly byte _unitId;

        private TcpClient _client;
        private NetworkStream _stream;
        private readonly object _gate = new object();

        private bool _lastOpOk = true;
        private ushort _transaction;
        private int _failCount;
        private DateTime _lastFailLog = DateTime.MinValue;

        /// <summary>Last connection failure, for PLC Diagnostics.</summary>
        public string LastConnectError { get; private set; }

        public DeltaModbusTcp(string endpoint)
        {
            // "host:port:unit" with everything after the host optional
            var parts = (endpoint ?? "").Split(':');
            _host = parts.Length > 0 && parts[0].Trim().Length > 0 ? parts[0].Trim() : "192.168.1.5";

            int p;
            _port = parts.Length > 1 && int.TryParse(parts[1].Trim(), out p) ? p : 502;

            byte u;
            _unitId = parts.Length > 2 && byte.TryParse(parts[2].Trim(), out u) ? u : (byte)1;
        }

        public string Endpoint { get { return _host + ":" + _port + ":" + _unitId; } }

        // ==================================================================
        // CONNECTION
        // ==================================================================

        public bool IsConnected
        {
            get
            {
                lock (_gate) return _client != null && _client.Connected;
            }
        }

        public bool IsHealthy { get { return IsConnected && _lastOpOk; } }

        public bool Connect()
        {
            lock (_gate)
            {
                Close();
                try
                {
                    _client = new TcpClient();
                    // A dead panel must not freeze the HMI for the OS default of ~20s.
                    // The scanner retries, so failing fast is what keeps the UI alive.
                    var async = _client.BeginConnect(_host, _port, null, null);
                    if (!async.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(1500)))
                    {
                        Close();
                        LastConnectError = "timed out opening " + _host + ":" + _port;
                        LogFailure();
                        return false;
                    }
                    _client.EndConnect(async);
                    _client.NoDelay = true;                 // Modbus frames are tiny; Nagle only adds latency
                    _client.ReceiveTimeout = 1000;
                    _client.SendTimeout = 1000;
                    _stream = _client.GetStream();

                    _lastOpOk = true;
                    _failCount = 0;
                    LastConnectError = "";
                    Kaban.Core.Helpers.AuditLogger.Log(
                        "[PLC DIAG] Delta Modbus TCP connected to " + Endpoint + ".");
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
            // First failure in full, then at most once a minute - a 1 Hz retry would
            // otherwise bury every other line in the audit log.
            if (_failCount++ != 0 && (DateTime.Now - _lastFailLog).TotalSeconds < 60) return;
            _lastFailLog = DateTime.Now;
            Kaban.Core.Helpers.AuditLogger.Log(
                "[PLC DIAG] Delta Modbus TCP " + Endpoint + " connect failed: " + LastConnectError +
                (_failCount > 1 ? " [repeated " + _failCount + " times]" : ""));
        }

        public void Disconnect()
        {
            lock (_gate) Close();
        }

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
            return value;    // 0 on failure, same shape as the other drivers
        }

        public bool TryRead(string address, out int value)
        {
            value = 0;
            var dev = DeltaAddress.Parse(address);
            if (dev == null) { _lastOpOk = false; return false; }

            lock (_gate)
            {
                if (!IsConnected && !Connect()) return false;

                try
                {
                    if (dev.IsBit)
                    {
                        byte[] bits;
                        if (!ReadCoils(dev.ModbusAddress, 1, out bits)) return false;
                        value = (bits[0] & 1) != 0 ? 1 : 0;
                    }
                    else
                    {
                        ushort[] words;
                        int count = dev.Is32Bit ? 2 : 1;
                        if (!ReadHoldingRegisters(dev.ModbusAddress, count, out words)) return false;
                        // 32-bit values are a LOW-WORD-FIRST pair on Delta, the same
                        // order MX Component reports for a Double Word.
                        value = dev.Is32Bit
                            ? unchecked((int)((uint)words[0] | ((uint)words[1] << 16)))
                            : (short)words[0];
                    }
                    _lastOpOk = true;
                    return true;
                }
                catch (Exception ex)
                {
                    _lastOpOk = false;
                    Close();
                    Kaban.Core.Helpers.AuditLogger.Log("Delta read " + address + " failed: " + ex.Message);
                    return false;
                }
            }
        }

        public int[] ReadMulti(string[] addresses)
        {
            int[] values;
            TryReadMulti(addresses, out values);
            return values;
        }

        /// <summary>
        /// Batch read. Contiguous words of the SAME device family are coalesced into
        /// one Modbus transaction, because the HMI scanner hands over a whole
        /// screenful at a time and one round trip per device would be far too slow.
        /// </summary>
        public bool TryReadMulti(string[] addresses, out int[] values)
        {
            values = new int[addresses == null ? 0 : addresses.Length];
            if (addresses == null || addresses.Length == 0) return true;

            bool allOk = true;
            var parsed = addresses.Select(DeltaAddress.Parse).ToArray();

            lock (_gate)
            {
                if (!IsConnected && !Connect()) return false;

                // --- words, grouped into runs of adjacent registers ---
                var wordIndexes = Enumerable.Range(0, parsed.Length)
                                  .Where(i => parsed[i] != null && !parsed[i].IsBit)
                                  .OrderBy(i => parsed[i].ModbusAddress)
                                  .ToList();

                int at = 0;
                while (at < wordIndexes.Count)
                {
                    int start = parsed[wordIndexes[at]].ModbusAddress;
                    int end = start + (parsed[wordIndexes[at]].Is32Bit ? 2 : 1);
                    int last = at;

                    // Modbus caps a read at 125 registers; stop well before that.
                    while (last + 1 < wordIndexes.Count)
                    {
                        var next = parsed[wordIndexes[last + 1]];
                        int nextEnd = next.ModbusAddress + (next.Is32Bit ? 2 : 1);
                        if (next.ModbusAddress > end + 8) break;        // a real gap: two transactions is cheaper
                        if (nextEnd - start > 100) break;
                        end = Math.Max(end, nextEnd);
                        last++;
                    }

                    ushort[] block;
                    if (ReadHoldingRegisters(start, end - start, out block))
                    {
                        for (int k = at; k <= last; k++)
                        {
                            int idx = wordIndexes[k];
                            int off = parsed[idx].ModbusAddress - start;
                            values[idx] = parsed[idx].Is32Bit
                                ? unchecked((int)((uint)block[off] | ((uint)block[off + 1] << 16)))
                                : (short)block[off];
                        }
                    }
                    else allOk = false;

                    at = last + 1;
                }

                // --- bits, grouped the same way ---
                var bitIndexes = Enumerable.Range(0, parsed.Length)
                                 .Where(i => parsed[i] != null && parsed[i].IsBit)
                                 .OrderBy(i => parsed[i].ModbusAddress)
                                 .ToList();

                at = 0;
                while (at < bitIndexes.Count)
                {
                    int start = parsed[bitIndexes[at]].ModbusAddress;
                    int last = at;
                    while (last + 1 < bitIndexes.Count &&
                           parsed[bitIndexes[last + 1]].ModbusAddress - start < 512) last++;

                    int count = parsed[bitIndexes[last]].ModbusAddress - start + 1;
                    byte[] packed;
                    if (ReadCoils(start, count, out packed))
                    {
                        for (int k = at; k <= last; k++)
                        {
                            int idx = bitIndexes[k];
                            int off = parsed[idx].ModbusAddress - start;
                            values[idx] = (packed[off / 8] >> (off % 8)) & 1;
                        }
                    }
                    else allOk = false;

                    at = last + 1;
                }

                // an address we could not parse is a failure, not a zero
                if (parsed.Any(p => p == null)) allOk = false;
                _lastOpOk = allOk;
                return allOk;
            }
        }

        public int Write(string address, int value)
        {
            var dev = DeltaAddress.Parse(address);
            if (dev == null) { _lastOpOk = false; return -1; }

            lock (_gate)
            {
                if (!IsConnected && !Connect()) return -1;

                try
                {
                    bool accepted;
                    if (dev.IsBit)
                    {
                        accepted = WriteSingleCoil(dev.ModbusAddress, value != 0);
                    }
                    else if (dev.Is32Bit)
                    {
                        accepted = WriteMultipleRegisters(dev.ModbusAddress, new[]
                        {
                            (ushort)(value & 0xFFFF),
                            (ushort)((value >> 16) & 0xFFFF)
                        });
                    }
                    else
                    {
                        accepted = WriteSingleRegister(dev.ModbusAddress, (ushort)(value & 0xFFFF));
                    }
                    // The link is fine either way - the device ANSWERED. But a write
                    // it refused (Modbus exception: bad address, read-only area) was
                    // reported as success, so the operator saw nothing wrong while
                    // the machine never received the value.
                    _lastOpOk = true;
                    return accepted ? 0 : -1;
                }
                catch (Exception ex)
                {
                    _lastOpOk = false;
                    Close();
                    Kaban.Core.Helpers.AuditLogger.Log("Delta write " + address + " failed: " + ex.Message);
                    return -1;
                }
            }
        }

        // ==================================================================
        // MODBUS TCP FRAMING
        // ==================================================================

        private bool ReadCoils(int start, int count, out byte[] bits)
        {
            var resp = Transact(0x01, Be(start).Concat(Be(count)).ToArray());
            bits = null;
            if (resp == null || resp.Length < 2) return false;
            int len = resp[1];
            if (resp.Length < 2 + len) return false;
            bits = new byte[Math.Max(len, (count + 7) / 8)];
            Array.Copy(resp, 2, bits, 0, len);
            return true;
        }

        private bool ReadHoldingRegisters(int start, int count, out ushort[] words)
        {
            var resp = Transact(0x03, Be(start).Concat(Be(count)).ToArray());
            words = null;
            if (resp == null || resp.Length < 2) return false;
            int len = resp[1];
            if (resp.Length < 2 + len || len < count * 2) return false;
            words = new ushort[count];
            for (int i = 0; i < count; i++)
                words[i] = (ushort)((resp[2 + i * 2] << 8) | resp[3 + i * 2]);   // Modbus is big-endian
            return true;
        }

        // Each returns false when the device answered with a Modbus exception
        // (Transact returns null and logs it).
        private bool WriteSingleCoil(int address, bool on)
        {
            return Transact(0x05, Be(address).Concat(new byte[] { (byte)(on ? 0xFF : 0x00), 0x00 }).ToArray()) != null;
        }

        private bool WriteSingleRegister(int address, ushort value)
        {
            return Transact(0x06, Be(address).Concat(Be(value)).ToArray()) != null;
        }

        private bool WriteMultipleRegisters(int address, ushort[] values)
        {
            var body = new List<byte>();
            body.AddRange(Be(address));
            body.AddRange(Be(values.Length));
            body.Add((byte)(values.Length * 2));
            foreach (var v in values) body.AddRange(Be(v));
            return Transact(0x10, body.ToArray()) != null;
        }

        private static byte[] Be(int v) { return new[] { (byte)((v >> 8) & 0xFF), (byte)(v & 0xFF) }; }

        /// <summary>
        /// One Modbus TCP request/response. Returns the PDU after the function code,
        /// or null when the device answered with an exception. Caller holds _gate.
        /// </summary>
        private byte[] Transact(byte function, byte[] payload)
        {
            if (_stream == null) throw new InvalidOperationException("not connected");

            unchecked { _transaction++; }
            var frame = new List<byte>();
            frame.AddRange(Be(_transaction));           // transaction id
            frame.AddRange(Be(0));                      // protocol id, always 0
            frame.AddRange(Be(payload.Length + 2));     // length of unit id + PDU
            frame.Add(_unitId);
            frame.Add(function);
            frame.AddRange(payload);

            _stream.Write(frame.ToArray(), 0, frame.Count);
            _stream.Flush();

            var header = ReadExactly(7);                // MBAP header
            int length = (header[4] << 8) | header[5];
            if (length < 1 || length > 260) throw new InvalidOperationException("bad Modbus length " + length);

            var pdu = ReadExactly(length - 1);          // unit id already consumed
            if (pdu.Length == 0) throw new InvalidOperationException("empty Modbus response");

            if ((pdu[0] & 0x80) != 0)
            {
                // an exception response is the device saying "that address is wrong",
                // which must not be reported as a value of zero
                Kaban.Core.Helpers.AuditLogger.Log(
                    "Delta Modbus exception 0x" + (pdu.Length > 1 ? pdu[1] : 0).ToString("X2") +
                    " for function 0x" + function.ToString("X2") + " on " + Endpoint + ".");
                return null;
            }
            return pdu;
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
    /// Delta device name -> Modbus address.
    ///
    /// The bases below are Delta's published mapping for the AS series (and the
    /// same for Ethernet DVP). They are the FIRST thing to check if a value reads
    /// back wrong on a family this has not been tried against - the protocol code
    /// above is generic Modbus and is far less likely to be at fault.
    /// </summary>
    internal class DeltaAddress
    {
        public bool IsBit;
        public bool Is32Bit;
        public int ModbusAddress;

        private static readonly Regex Pattern =
            new Regex(@"^\s*([A-Za-z]+)\s*(\d+)\s*$", RegexOptions.Compiled);

        // device letter -> (modbus base, is a bit device)
        private static readonly Dictionary<string, KeyValuePair<int, bool>> Map =
            new Dictionary<string, KeyValuePair<int, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            { "M", new KeyValuePair<int, bool>(0x0000, true) },   // auxiliary relays
            { "S", new KeyValuePair<int, bool>(0x4000, true) },   // step relays
            { "X", new KeyValuePair<int, bool>(0x6000, true) },   // inputs  (octal, see below)
            { "Y", new KeyValuePair<int, bool>(0xA000, true) },   // outputs (octal, see below)
            { "T", new KeyValuePair<int, bool>(0xE000, true) },   // timer contacts
            { "C", new KeyValuePair<int, bool>(0xF000, true) },   // counter contacts
            { "D", new KeyValuePair<int, bool>(0x0000, false) },  // data registers
        };

        public static DeltaAddress Parse(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;

            var m = Pattern.Match(address.Trim());
            if (!m.Success) return null;

            string device = m.Groups[1].Value.ToUpperInvariant();

            // A "D100.32" style suffix is not Delta notation, so 32-bit width is
            // decided by the caller's DataType, not by the address. Treated as a
            // single register here; the monitor's DataType column is display only.
            bool is32 = false;

            KeyValuePair<int, bool> entry;
            if (!Map.TryGetValue(device, out entry)) return null;

            int number;
            // X and Y are OCTAL on Delta exactly as they are on Mitsubishi - X10 is
            // the ninth input, not the tenth. Getting this wrong shifts every I/O
            // point by a few and is very hard to see on screen.
            bool octal = device == "X" || device == "Y";
            if (octal)
            {
                if (m.Groups[2].Value.Any(c => c > '7')) return null;   // not a valid octal point
                number = Convert.ToInt32(m.Groups[2].Value, 8);
            }
            else if (!int.TryParse(m.Groups[2].Value, NumberStyles.Integer,
                                   CultureInfo.InvariantCulture, out number)) return null;

            return new DeltaAddress
            {
                IsBit = entry.Value,
                Is32Bit = is32,
                ModbusAddress = entry.Key + number
            };
        }
    }
}

