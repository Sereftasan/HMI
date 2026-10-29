# Alarm texts still needing translation

Since 30 October the alarm texts are rows of each machine's Translations.csv
(`Alarm_<Machine>.<TagCode>`, moved there from Alarms.json by
`tools\alarm_texts_to_translations.py`, the Turkish from the Industry 4 program's
Translation_JSON.csv where it has the alarm). The alarms below had no row in
Translation_JSON.csv: their Turkish is the workbook's (often the PLC's tag name) and
RU/DE/IT/ES show English - fill them on HMI Settings > Translation (or with its AI
prompt).

## FA2010  (6 of 157)
  M819   OKayitX_Ariza
  M832   Saw Dust Conveyor  Inverter Alarm
  M961   CikisOnDikey_YukariCikamadi
  M962   CikisOnDikey_Asagi_Inemedi
  M963   OKayitKertmeUnitesi_YukariCikamadi
  M964   OKayitKertmeUnitesi_Asagi_Inemedi

## SB3012  (66 of 114)
  M809   Robot X Servo SError
  M813   Cutting Z Servo Error
  M845   Profil_Algilama_Piston_Sw_Ariza
  M849   Cutting 45 Left Top Roller Clamping Failure
  M850   Cutting 45 Left Top Roller Releasing Failure
  M851   Kesim_UST_KUCUK_SIKMADI
  M852   Kesim_UST_KUCUK_ACMADI
  M853   Cutting 45 Middle Front Roller Moving FW Failure
  M854   Cutting 45 Middle Front Roller Moving Back Failure
  M855   Cutting 45 Right Front Clamping Failure
  M856   Cutting 45 Right Front Releasing Failure
  M857   Cutting 45 Right Top Big Clamping Failure
  M858   Cutting 45 Right Top Big Releasing Failure
  M859   Cutting 45 Right Front Roller Moving FW Failure
  M860   Cutting 45 Right Front Roller Moving Back Failure
  M861   Cutting 90 Roller Moving Up Failure
  M862   Cutting 90 Roller Moving Down Failure
  M863   Cutting 90 Front Clamping Failure
  M864   Cutting 90 Front Releasing Failure
  M865   Cutting 90 Top Clamping Failure
  M866   Cutting 90 Top Releasing Failure
  M867   Wastage Pushing Cylinder Moving FW Failure
  M868   Wastage Pushing Cylinder Moving Back Failure
  M870   Exit Robot Releasing Failure
  M871   Exit Robot Moving Up Failure
  M872   Exit Robot Moving Down Failure
  M873   Exit Robot Stopper Positioning Failure
  M874   Exit Conveyor Stopping SW Error
  M875   Profile is Detected on the Machine
  M876   The Bar Length is Too Short to Process
  M877   Milling Gcode Starting Y and Z Values are Zero
  M879   Entry Robot Outside The Limit
  M880   Milling Angle Outside The Limit
  M881   Milling Y1 Outside The Limit
  M882   Milling Z1 Outside The Limit
  M883   Cutting Z Outside The Limit
  M884   Exit Robot Outside The Limit
  M885   Milling Y2 Outside The Limit
  M886   Milling Z2 Outside The Limit
  M887   Milling Z1 + Milling Z2 Crushing Limit
  M888   Profile is Detected in Exit Robot
  M889   Cutting 45 Left Top Big Clamping Failure
  M890   Cutting 45 Left Top Big Releasing Failure
  M891   Cutting 45 Left Top Small Clamping Failure
  M892   Cutting 45 Left Top Small Releasing Failure
  M893   Cutting 45 Right Top Small Clamping Failure
  M894   Cutting 45 Right Top Small Releasing Failure
  M895   Milling Front Plate Moving Back Failure
  M896   Milling Front Plate Moving FW Failure
  M897   Milling Front Clamping Failure
  M898   Milling Front Releasing Failure
  M899   90 Saw Down Movement Failure
  M900   90 Saw Up Movement Failure
  M901   Cutting Foil Roll Moving FW Failure
  M902   Cutting Foil Roll Moving Back Failure
  M903   Cutting Foil Roll Moving Up Failure
  M904   Cutting Foil Roll Moving Down Failure
  M906   Conveyor Roller Moving Up Failure
  M907   Conveyor Roller Moving Down Failure
  M908   KesimSolAltTabla_SensordenKurtardi_Sikmada
  M909   KesimSolDayama_SensordenKurtardi_Sikmada
  M910   KesimSagDayama_SensordenKurtardi_Sikmada
  M911   KesimSolAltTabla_SensordenKurtardi_Sikmasiz
  M912   KesimSolDayama_SensordenKurtardi_Sikmasiz
  M913   KesimSagDayama_SensordenKurtardi_Sikmasiz
  M914   Kesimde_FolyoRulosuGerideDegil

## SB3015  (8 of 90)
  M802   Eksen2_Ariza
  M805   Eksen5_Ariza
  M810   Eksen10_Ariza
  M813   Eksen13_Ariza
  M814   Eksen14_Ariza
  M817   Eksen15_Ariza
  M818   Eksen16_Ariza
  M832   Saw Dust Conveyor  Inverter Alarm

