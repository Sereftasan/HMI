# Industry 4 logging in the HMI - a plan (5 November)

What you asked for: "combine HMI and PLC logging together so we could just take time sensitive logs from PLC (to prevent info loss
caused by the HMI connection failures) and log the rest of them from the HMI itself. So we can create triggers to take logs for each
axis unit or other pneumatic controls as well." And: "the user would see logs for only machine by machine, they should not need to
know the PLC-HMI distinction".

Nothing below is built yet. The questions are at the end.

## Where we are

- Each machine keeps its log database (HMI Settings > Logs and Reports: its database file). A record is a `main_log` row (its code,
  its time) with its values in `log_details` - the Industry 4 service's tables. The dashboard and the reports read them.
- The PLC records go there through its buffer (R27000 on FA2010; D8000 in MachineLog_DeviceList.xlsx), taken by the Industry 4
  service or by the HMI (Industry 4 logging in the HMI). Which records the PLC makes: the Options page's Industry 4 Logging frame.
- The HMI's own log (sign-ins, values written, settings changed) goes into the **main machine's** database as codes 1000 + its kind.
  The dashboard's All logs shows a Source column and filter: "HMI" or the machine.

## The plan

1. **One log per machine, from both sources.** A record the HMI makes is written into the machine's database as the PLC's are: the
   same tables, the same codes (MachineLog_DeviceList.xlsx), its time the HMI's clock. Where it came from stays a detail only for
   service work. The dashboard, the reports and the lists show machines only: the Source filter becomes the machine (FA2010, H500,
   H800), and an HMI event that concerns a machine (a value written to its PLC, a profile loaded for it, its options or parameters
   changed, its alarm acknowledged) is in that machine's log.

2. **The PLC keeps what must not be lost.** States 0 - 5 (the state charts), 8 Cutting done (production) and the error records (4/14,
   with the error bits at that moment) stay in the PLC's buffer, so a dropped connection loses nothing: they are taken when it is back.

3. **The HMI makes the rest, by triggers.** A unit on a page (axis unit, cylinder unit, inverter unit, or any bit/word) gets a log rule
   in the designer - its own property group, like Conditions:
   - **Record**: the code it writes (12 Axis overload, 13 Cylinder operation overtime, 6 Profile changed ...).
   - **When**: a bit rising (or falling); a value over/under a limit for a time (axis current over 150 % for 2 s); a movement taking
     longer than its time (the HMI already times every cylinder movement for its own alarms).
   - **Values**: the devices read at that moment (axis no, position, current; cylinder no, overtime amount; profile code).
   - Switched on or off in the Industry 4 Logging frame, as the PLC's records are (a row each).
   Periodic axis data stays off (too much); an axis can instead log a snapshot when something happens to it (overload, alarm).

4. **Nothing changes for the service.** With Industry 4 logging in the HMI off, the Industry 4 service works as today; the tables
   do not change, so old databases and the reports keep working.

## Questions

1. HMI events that concern no single machine (program opened/closed, user changed, HMI settings): into every machine's log, or
   shown with each machine but kept once?
2. Which HMI records first, and their limits: 12 Axis overload (current over what %, for how long?), 13 Cylinder operation
   overtime (the HMI's alarm delay, or a time of its own?), others?
3. The details FA2010's PLC logs now (6 Profile changed, 15, 16, 20, 22): stay with the PLC, or move to HMI triggers?
4. Are the codes of MachineLog_DeviceList.xlsx (17 - 19, 24 - 28, and the "Other Logs (w/o PLC involvement)" list) the ones the HMI
   should use for its own records - with numbers given to that last list?
