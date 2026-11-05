# Industry 4 logging in the HMI - plan (5 November; your answers, the research and the first records added the same day)

What you asked for: "combine HMI and PLC logging together so we could just take time sensitive logs from PLC (to prevent info loss
caused by the HMI connection failures) and log the rest of them from the HMI itself. So we can create triggers to take logs for each
axis unit or other pneumatic controls as well", "the user would see logs for only machine by machine", and "make a
search about what logs would be useful for these controls so we could apply them".

Built (5 November, "Your suggestion"): section 4's first records, the older / new machine choice and the stop reasons - see section 5.

## 1. Decided

- **One log per machine.** The dashboard, its reports and the lists show machines only (FA2010, H500, H800) - never "HMI" or "PLC".
- **HMI events that concern no single machine** (program opened/closed, user changed, HMI settings): **stored once, shown with each
  machine's log.** An HMI event that concerns one machine (a value written to its PLC, its options or parameters changed, its alarm)
  is that machine's.
- **The PLC keeps the time-sensitive records** (its buffer survives a dropped connection): the machine states 0 - 5 (0 - 2 always on),
  8 Cutting done, the error records 4/14.
- **Older machines keep their PLC detail records** (FA2010: 6, 15, 16, 20, 22 - switched in the Industry 4 Logging frame).
  **New machines take the details from HMI triggers** instead. Per machine, by its definition; the user sees one list either way.
- **No old codes for the new records.** The numbers of MachineLog_DeviceList.xlsx were for the logging and dashboard programs to talk to
  each other; the new HMI does both, so its own records are named kinds (e.g. `axis.overload`), kept in the same database.
- **The PLC log area** (R27000) is the machine program's (`DashboardDefinition.LogArea`), set by the PLC programmer - not on the screen.

## 2. What is worth logging - the research

Sources are at the end. Vendor pages give methods, rarely limits. The "start" limits below are **ours, to tune per machine** -
except the +50 % of a cylinder's movement (the pneumatics vendors' example) and the inverter's life alarms (Mitsubishi's) - and are
best set from a baseline measured at commissioning (what the machine does when it is new).

### Servo axes (axis rows / axis units; the simple motion module's monitor data the HMI already reads)

| Record | When | Values | Why |
|---|---|---|---|
| Axis alarm snapshot | axis error, axis warning or servo alarm code turns non-zero (Md.23 / Md.24 / Md.114) | the code(s), position, speed, effective load, peak torque, regenerative load, what the machine was doing | the moment of a fault, kept - the drive recorder idea, on the HMI's side |
| Axis overload | effective load ratio (Md.110, 15 s average, rated = 100 %) over a limit (start: 100 %) for a time (start: 10 s), or peak torque ratio (Md.111) over its limit (start: 250 %) | axis, position, the two ratios, the limit | a sticking guide, a dry ball screw, a crash (code 12 of the old list) |
| Regenerative overload | regenerative load ratio (Md.109) over a limit (start: 80 %) | axis, ratio | braking resistor running hot - too fast decelerations or a heavy load |
| Load trend | once per shift (or per N moves) | per axis: highest effective load and peak torque, moves made, distance travelled | friction growing slowly = wear (Mitsubishi's machine diagnosis judges ball screw, belt and gear by friction and vibration against the new machine's values) |
| Travel and moves | daily | per axis: distance travelled, number of moves, number of references | usage toward lubrication and service (MR-J5 predicts failure from total travel against a rated life) |
| Reference lost / done | absolute position lost, or a reference completed | axis, position | why an axis needed referencing |

### Pneumatic cylinders (cylinder units - the HMI already times every movement for its own alarms)

| Record | When | Values | Why |
|---|---|---|---|
| Slow movement | a movement takes longer than its baseline by a margin (start: 50 %) - before it becomes an alarm | unit, direction, time taken, baseline | the drift every source names first: worn seals, a sticking valve, low flow or pressure; a 1 s stroke taking 1.5 s is the vendors' example |
| Cylinder operation overtime | the movement's alarm (its delay passed) | unit, direction, time, which sensor did not come | code 13 of the old list - the fault itself |
| Movement time trend | once per shift | per unit and direction: count, average and longest time | the baseline drifting, seen before anything fails |
| Cycles | daily | per unit: movements made (and the total) | toward the valve's and cylinder's rated life (manufacturers give B10 life in switching cycles - a valve example: 100 million) |
| Sensor fault | both end sensors on at once; an end sensor lost while held there; a sensor flickering | unit, the sensor, how often | a loose or failing sensor - or a clamp opening under load |
| Clamp not confirmed | a clamp's pressure switch not on while clamped | unit | a clamp that does not hold |

### Inverter motors (saw, milling, conveyors - inverter units)

| Record | When | Values | Why |
|---|---|---|---|
| Inverter fault snapshot | the fault code turns non-zero | the code, output current and frequency at that moment, run time | the moment of the fault |
| Load per cut | each cut (saw) / each milling | peak and average output current, the profile | the same cut taking more current = a dulling blade or tool; a cut far over the steady average (the wear-monitor patent: 50 - 100 % over it) = a blade near failure |
| Run hours and starts | daily | per motor: hours run, starts | motor and spindle service |
| Inverter life | the inverter's life alarm comes on (Mitsubishi 800 series: main circuit capacitor under 85 %, control capacitor or inrush relay under a year left, cooling fan under 50 % speed) | which part | replace before it fails |

### The machine (HMI-made, for new machines; older machines: the PLC's records as now)

| Record | When | Values | Why |
|---|---|---|---|
| Cycle / part | a profile's processing ends | profile code, length, cycle time, result | production and the cycle time against its ideal (OEE performance) |
| Downtime reason | the machine stops longer than a limit (start: 2 min) - the operator picks a reason on the screen | start, duration, category, reason | OEE availability: which stops cost most (a Pareto of reasons); short stops logged too |
| Quality | profile not recognised, measured outside tolerance, waste cut | the measured values | scrap and its causes (OEE quality) |
| Consumable changed | the operator says so (saw blade, tool, filter), or the counters reach a limit | which, the counts since the last change | blade and tool life in cuts |
| Operator actions | safety bypass on/off, manual mode, a jog, a parameter or option changed (the HMI's own log has the writes) | who, what | what was done to the machine before a fault |
| Supply faults | air pressure low, oil level low, emergency stop | duration | outside causes of stops |

## 3. How it would be built (after your choice of records)

1. **Log rules on the units** (designer properties, their own group like Conditions): the record, when (a bit rising/falling; a value over
   or under a limit for a time; a movement slower than its baseline or its alarm; once per shift/day for trends and counters), the values
   read at that moment. The common ones come built into the unit types (an axis unit knows its monitor data, a cylinder its timing).
2. **Baselines**: each unit learns its normal (movement times, loads) over its first days - or a "take as normal" button after service -
   and the limits are relative to it.
3. **One list in the Industry 4 Logging frame**: the PLC's records (older machines) and the HMI's (new machines) as switch rows; a rule's
   limit editable for Maintenance.
4. **The dashboard**: a machine's log shows its records from both; trends (movement times, loads, counts) as charts per unit.

## 4. Your choice (5 November)

- First: "Your suggestion" - the cylinders' slow movement, overtime and movement time trend, the axis alarm snapshot and overload, the
  inverter fault snapshot and load per cut.
- Older or new machine: "this same HMI program is gonna be deployed to the currently operating old machines as well as the new producing
  ones. So it should be selected at the HMI setting's option page".
- A long stop: "Yes, we could ask it to enter the reason".

## 5. What is built (5 November)

- HMI Settings > Options: **Machine details from the HMI (new machines)** - off: an older machine (its PLC logs the details); on: a new
  machine (the HMI takes them). Only with the Industry 4 logging in the HMI on.
- The HMI's records (`KabanHMI\Dashboard\MachineRecords.cs`), written into the machine's log database as its own records (codes 1100+,
  named - the dashboard lists them as the machine's events, in each language):

| Code | Record | Limit (default) |
|---|---|---|
| 1101 | Slow cylinder movement | slower than its normal by 50 % (and 0.3 s); its normal learned from its first 20 movements |
| 1102 | Cylinder movement not confirmed | its alarm's delay |
| 1103 | Cylinder movement times (per shift) | - |
| 1111 | Axis alarm | - |
| 1112 | Axis overload | effective load 100 % for 10 s, or peak torque 250 % |
| 1121 | Inverter fault | - |
| 1122 | High motor load | peak current over its normal peak by 50 % (needs the inverter's current word) |
| 1123 | Motor loads (per shift) | - |
| 1131 | Machine stop | asked after 5 minutes: nine reasons (planned, setup, unplanned) |

- Each switched and limited in the machine's Industry 4 Logging frame (Machine Parameters > Options), with "Learn the normal times again".
- The cylinders are timed for the records even with the HMI's cylinder alarms off; the axes' loads (Md.109 - Md.111) and the inverters'
  current are read only while the records are taken.
- The machine's own records by HMI triggers (your answer: "We need to define a trigger device and additional info devices for each
  one ... just like 'EnableTrigger' ... a collection lamp HMI and numerical display HMI controls"): LogRecordHMI rows in the Industry 4
  Logging frame - code, trigger, values - shown on a new machine instead of the PLC's switches of the same records. FA2010: 15, 16, 22
  (from your PLC program's MakineLogKayit calls), 6 and 20 (your answer: "Their trigger devices are given at the DeviceList ... files
  (from L1000 to ...)" - L1006, L1020). The measured width and height: D481 / D482 (FA2010_DeviceList.xls, "Okunan Profil Genişliği /
  Yüksekliği"). FireBoyu, BoyOkundu, ProfilOlcum_OK, Profil_Dogru, the profile code (6) and the required switch state (20) still need
  their devices - you set them in the Options page's designer.
- Not yet: the
  inverters' current needs the PLC to read it (FA2010 has no current word yet); the axis load trend, travel and moves, the cylinders'
  cycles and sensor faults, the cycle / quality / consumable records of section 2.

## Sources

- Mitsubishi Electric, GOT2000 servo machine diagnosis: https://us.mitsubishielectric.com/fa/en/products/hmi/human-machine-interface/got2000/servo-solutions/machine-diagnosis/
- Mitsubishi Electric, GOT2000 servo failure prediction: https://us.mitsubishielectric.com/fa/en/products/hmi/human-machine-interface/got2000/servo-solutions/failure-prediction/
- Mitsubishi Electric, MR-J5 maintainability: https://mitsubishifa.co.th/en/products/drv/servo/pmerit/mr_j5/amp/f_maintainability/
- Mitsubishi Electric, technical report 174 (MR-J5 failure prediction): https://www.advance.mitsubishielectric.com/advance/pdf/2021/174_TR2.pdf
- Mitsubishi Electric, inverter predictive maintenance: https://us.mitsubishielectric.com/fa/en/products/drv/inv/pmerit/prevention/predictive/
- Mitsubishi Electric, FR-E800 maintainability: https://www.mitsubishielectric.com/fa/products/drv/inv/pmerit/fr_e/e800/e806.html
- Mitsubishi QD77MS manual, monitor data Md.109 - Md.114: https://manualsonline.com/manuals/mfg/mitsubishi/qd77ms4.html?p=289
- Machine Design, predictive intelligence for pneumatics (Bimba): https://www.machinedesign.com/automation-iiot/sensors/article/21833783/technology-provides-predictive-intelligence-for-pneumatics
- Plant Engineering, pneumatics maintenance (Aventics): https://www.plantengineering.com/?p=21409
- Machine Building, smart sensors for cylinders (Contrinex): https://www.machinebuilding.net/smart-sensors-provide-a-built-in-diagnostic-solution-for-cylinders
- Industrial Monitor Direct, cylinder timing drift: https://industrialmonitordirect.com/blogs/knowledgebase/slc-504-pneumatic-cylinder-timing-drift-troubleshooting
- FourJaw, downtime categories: https://help.fourjaw.com/knowledge/logging-downtime-and-categories
- Oxmaint, OEE data collection checklist: https://oxmaint.com/industries/manufacturing-plant/oee-data-collection-downtime-tracking-checklist
- Cutting tool wear monitor patent US4694686: https://patents.google.com/patent/US4694686
