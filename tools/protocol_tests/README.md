# Protocol tests for the Siemens and Delta drivers

Neither driver has been run against real hardware - nobody here has an S7 or a
Delta CPU. What CAN be checked without one is the thing most likely to be wrong:
the bytes on the wire. These mocks are servers that speak the real protocols and
**assert** on malformed frames, so a driver that sends nonsense fails loudly
rather than appearing to work.

Between them they cover the parts that are easy to get backwards:

| Checked | Why it matters |
|---|---|
| Modbus function codes 01/03/05/06/10 | a read of the wrong table returns plausible rubbish |
| Modbus big-endian framing | byte-swapped words look like huge numbers |
| Delta `X`/`Y` are **octal** | `X10` is the 8th input; decimal shifts every I/O point |
| signed 16-bit decode | `0xFFFE` must be `-2`, not `65534` |
| batch coalescing | adjacent registers must become ONE transaction |
| S7 COTP TSAP from rack/slot | a wrong slot is refused at connect, not later |
| S7 setup PDU negotiation | the agreed size caps every later read |
| S7 **big-endian** words/dwords | opposite byte order to Mitsubishi and Delta |
| S7 bit vs byte transport sizes | response lengths are in BITS for both |
| bare `M10` refused | ambiguous - reading byte 10 instead would be silent and wrong |

## Running them

Needs `mono-mcs` and the built driver DLLs. From the repository root:

```
mcs -target:library -out:/tmp/build/Kaban.Core.dll $(find Kaban.Core -name '*.cs')
mcs -target:library -out:/tmp/build/Kaban.Drivers.Delta.dll \
    -r:/tmp/build/Kaban.Core.dll $(find Kaban.Drivers.Delta -name '*.cs')
mcs -out:TDelta.exe -r:/tmp/build/Kaban.Core.dll \
    -r:/tmp/build/Kaban.Drivers.Delta.dll tools/protocol_tests/TDelta.cs

cd tools/protocol_tests && python3 run_delta.py     # and run_s7.py
```

`run_s7.py` binds port 102, which the S7 protocol fixes and which needs root.

## What they do NOT prove

- That a real CPU accepts these frames. A real S7 refuses PUT/GET by default and
  an optimised DB has no byte addresses at all; a real Delta may use different
  Modbus bases per family. Both are noted in the driver files.
- Timing under load, reconnection behaviour on a flaky network, or anything
  about how a particular CPU handles a partial write.

Treat first commissioning as a bring-up. PLC Diagnostics answers "does the
socket open"; one bit read in the monitor answers "is the address mapping right".

