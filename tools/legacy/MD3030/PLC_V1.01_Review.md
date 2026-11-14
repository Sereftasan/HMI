# MD3030 PLC program V1.01 - what reading it turned up (13 November)

The user asked for the program to be refactored, its potential bugs fixed and its unneeded
parts deleted. A GX Works3 project cannot be written from here (it is read through its
databases - tools/gx3_program.py - and a project written back that way would not be one GX
Works3 can be trusted to open, let alone a machine to run), so the PLC program itself is not
changed. This is the list to change it by, in GX Works3; every line below is quoted from
`PLC_V1.01_Program.txt`.

## Copy-and-paste slips (the manual cylinder commands, POU "Ust Baskilar ... Bakim Uyarilari")

* **Stripping knife forward, heads 3 and 4** (rung 26, `MAN_Siyirma_Bicak_Ileri` M222):

      SET(Kafa3_Sol_Aktif AND X2_AxisPoz < KafaX_Bekleme_Poz + K50000, MAN_Siyirma_Bicak2_Ileri);
      SET(Kafa4_Sol_Aktif AND X2_AxisPoz < KafaX_Bekleme_Poz + K50000, MAN_Siyirma_Bicak2_Ileri);

  Heads 3 and 4 test head 2's X position and set head 2's knife: `MAN_Siyirma_Bicak3_Ileri` /
  `4_Ileri` (W161.E, W171.E) are set nowhere, so the manual command moves the knives of heads 1
  and 2 only. Meant: `X3_AxisPoz ... MAN_Siyirma_Bicak3_Ileri`, `X4_AxisPoz ... Bicak4_Ileri`.
  The same in rung 27 (`MAN_Siyirma_Bicak_Geri` M223): both lines reset `MAN_Siyirma_Bicak2_Ileri`.
* **Support plate down** (rung 18, `MAN_Dayama_AltRezistans_Asagi` M233): the four resets of
  `..._Yukari` all test `Kafa1_Sol_Aktif` (heads 2 - 4 should test their own). Harmless while
  head 1 is always active - it is set with the others at the first scan - but not what was meant.

## Named, but doing nothing in V1.01

* `MAN_Frezeler_Stop` M214: in no rung.
* `_MAN_Konveyor_Yukari` / `_Asagi` M206 / M207, `_MAN_TablaHareket` M203, `_MAN_DayamaUnitesi_...`
  M224 / M225, `_MAN_DayamaPlakasi_...` M226 / M227, `_MAN_AltRezistans_Yukari` M235: in no rung
  (the underscore marks them as retired). The HMI's Conveyor Up/Down buttons write M207 / M206.
* The body brakes' manual labels (`MAN_GovdeX / Y1 / Y2_Fren_SIK`) have no device: the brakes
  are released whenever the machine is out of automatic. The old HMI's M260 - M262 are not in the
  program.
* The brake rungs' timers carry another axis's name (Y1's rung drives `GovdeX_FrenAcmaGecikme`,
  Y2's `GovdeY1_...`, X's `GovdeY2_...`) - worth a look at which rung each timer is read in.

## Labels no rung names

`PLC_V1.01_UnreferencedLabels.txt`: 299 of the 1261 global labels that have a device are named in
no rung and no ST line (35 PC_, 9 MAN_, the rest values and flags). Some are read or written by
the HMI only, and the program may reach others by address - a list to go through with GX Works3's
cross reference, not one to delete from.

## How sure this is

The inline ST boxes and the ST POUs are the program's own text. The ladder rungs are rebuilt
from each rung's grid (contacts, coils, vertical links; the lines between them are not stored),
so a rung's condition here can be wrong where a blank cell and a line look alike - nothing above
rests on a rebuilt rung except the brake timers' names.
