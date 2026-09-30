# The Industry 4 dashboard (DashboardApp3) — what it does, for the HMI's own

The user's Streamlit/Python program (DashboardApp3.py, config_editor.py,
run_report_only.py, config.json, databases.json, sample.json, Translation.csv,
Translation_JSON.csv), sent 30 September 2026, to be combined with the HMI's own
logging. This is the specification the HMI version follows. The files in this
folder are the user's, kept as they came (the Python sources are summarised here).

## Data

- SQLite (`config.database_path`, e.g. `C:/Industry4/Service/database.sqlite`), written today
  by a service that watches the PLC:
  - `main_log(id, state_date TEXT 'YYYY-MM-DD HH:MM:SS' in UTC, state_code INT)`
  - `log_details(id, main_log_id, log_description TEXT, deger REAL, str_deger TEXT)`
  - indexes it adds: main_log(state_date), main_log(state_code, state_date), log_details(main_log_id)
- `sample.json` = the STATE MAP (FA2010): StateCode, Description, Active, ActiveDevice (L1000+code,
  the PLC latch that raises it), PreviousCode (a continuation record of another code), Parameters
  (Description, Bit32, Multiplier, Bits = names of the bits of an error word).
  - codes 0..5 are MACHINE STATES: 0 Machine is closed, 1 Operation is not active,
    2 Operation is active, 3 On pause mode, 4 There is an error, 5 Emergency stop;
  - everything else is an EVENT (6 Profile changed, 7 Milling done, 8 Cutting done (Processing
    Centre), 9/12 Cutting done (Cutting Machine), 10/11 Welding done, 13 Cylinder overtime,
    15 Waste profile, 16 Bar length, 22 Profile measured, 100-107/150-157 axis data);
  - code 4 rows are also ALARMS: their details are the error bits that were on (details
    "<bit name>: 1").
- Colours: active #2ecc71, not active #5C7BAA, pause #ffff00, error #f39c12, emergency #ff0000,
  closed #95a5a6.
- Times in the database are UTC; shown in `config.time_zone` (Europe/Istanbul).
- `databases.json`: other sources through adapters — `sqlite_std` (tables main_log/log_details),
  `csv_folder` (files main_log_*.csv + log_details_*.csv, datetime "%d.%m.%Y %H:%M:%S"; or a
  vendor's event_log_*.csv with Time/EventID/Message mapped to state_date/state_code/RawDescription).

## Processing (per selected range)

1. The last STATE row before the range start is loaded too (the state in force at the start).
2. Description = state map description, translated (Translation_JSON.csv: Reference;English;...).
3. Details of a row: "<translated log_description>: <value>" joined with "; " (value = str_deger,
   else deger; an empty or "0.0" value gives the description alone).
4. States: consecutive duplicates dropped; each lasts until the next state (the last one to the
   range end); clipped to the range; Duration in minutes; zero-length dropped.
5. Events: non-state rows in the range, with their details. "Barcoded cutting logs only": a
   code 8 event counts only when it has a "Profile Data" detail.
6. Alarms: code 4 rows in the range; details with ": 1" / ": 1.0" removed.
7. Shift filter: the range cut into each day's shift window (a shift ending at or before its
   start ends the next day); states clipped to each window.

## Screen

- Sidebar: Language; Auto-Report History (the schedule's past report end times → that report's
  range); Latest (15-Minute, 30-Minute, Hourly, 8-Hour, Daily, Weekly, Monthly, 3-Month, Annual) with
  a refresh; manual Start/End date and time; Shift selection (All Shifts or one) with an editor
  (add/remove/change shifts) and Shift metrics (per shift: Active %, one column per event type
  with counts, Alarm count — by the start time of each row); Export Report; Open Export Folder.
- Main: title "Local Machine Dashboard", "Logs interval: start - end" (and the active shift);
  collapsible sections (open/closed remembered; each can be switched off in the settings):
  - Machine State Time Distribution — pie of minutes per state, state colours;
  - Machine State Timeline — Gantt of the states;
  - State Logs — #, Timestamp, Description, Duration (min, 3 decimals);
  - Event Occurrences (log scale) — bar per event description;
  - Cutting Details (Total Length: n m) — from code 8 "Profile Data" (str_deger split by ";":
    [1] length/10 mm, [3] left angle/10, [4] right angle/10), grouped by rounded length and angles
    with a quantity, longest first;
  - Event Logs — #, Timestamp, Description, Details; filter: multi-select of descriptions with counts;
  - Alarm Logs — #, Timestamp, Details; filter: multi-select of alarm names (each ";" item's part
    before ":") with counts.

## Reports

- CSV (all fields quoted) into `export_directory`: `event_log_<start>_<end>.csv` (Timestamp,
  Description, Details), `alarm_log_...` (Timestamp, Details), `state_log_...` (Timestamp, EndTime,
  Description, Duration), optionally `main_log_...` / `log_details_...` (the tables, codes as
  descriptions, dates "%d.%m.%Y %H:%M:%S"); stamps `%Y%m%d_%H%M` (a manual export's end minus a second).
- Auto-report: schedule type Daily (times), Weekly (weekdays + time), Monthly (month days + time),
  Annual (MM-DD dates + time); a report covers previous scheduled time → scheduled time.
  `required_report_end_date` is the next report due; when the PC was off past it, a catch-up report
  is made first, then the latest; then the next due time is stored. history_days / history_limit
  bound the history list.
- Settings (config_editor.py): database path, time zone, barcoded-only, dashboard language,
  section switches, export directory, report language, auto-report period and schedule details,
  which report files.

## In the HMI (done 30 September)

The Dashboard page (`KabanHMI/Dashboard/`, mounted through `StandardScreens.Options.Dashboard`) does all of the
above: the same settings file keys and layout, the same processing, the same report files (compared byte for byte
with DashboardApp3.py on a generated log), the automatic reports with their catch-up, and the settings editor as a
Machine Parameters tab. Differences, on purpose: the page follows the HMI's language; a night shift that began the
day before the range counts; a detail with neither value shows its name alone; shift editing, the folder and the
settings need Maintenance; the shift metrics are at the top of the page; ranges over 1.5 million entries are refused.
Not used: `databases.json` (the other sources). See REVIEW 4u.

## Where it goes next

- The HMI already knows the states, the alarms and most events: it can write the same two tables
  itself (moving the logging off the PLC later), with the alarm texts from its own Alarms.json and
  translations — one list of alarms instead of the PLC's bit lists and this program's copies.
