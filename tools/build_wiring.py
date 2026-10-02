#!/usr/bin/env python3
"""
Builds Kaban.Machines.FA2010/Wiring.csv - WHERE EACH PHYSICAL POINT IS ON THE
MACHINE'S DRAWINGS: the sensor's tag and wire, the output's relay and valve
coil, the pneumatic valve, hoses and cylinder. The cylinder help (the tailored
troubleshooting manual) and the Inputs / Outputs page show it, so the
electrician does not have to leaf through 49 pages to find wire 119.

Sources
  * The electrical drawing "ZC 2010-FA 2010-FA 3010", project 134581, revision 9
    (03.09.2026), 40 pages - transcribed below (ELECTRICAL_*). "Sheet 12.6" is
    page 12, column 6 - how the drawing's own cross-references write it.
  * The pneumatic drawing, project 134628, revision 9, 9 pages - transcribed
    below (PNEUMATIC). "2 E3" is page 2, zone E3.
  * tools/FA2010_DeviceList.xls, sheet PLC_Labels: which manual command bit
    drives which output (MAN_GirisKonv_Sikma6_SIK on M688 -> OUT_GirisKonv_
    Sikma6_SIK on Y12), matched by the label's name.
  * Kaban.Machines.FA2010/IoList.csv: the relay each point is read at (M234).

The drawings cover the machine family; points the HMI lists that this revision
does not draw (input conveyor clampings 1 - 5, for one) have no row.
Point numbers are the PLC's: the extension modules continue the base unit's
X/Y numbering in their order on the bus (sheet 36): U7 X40, U8 X60, U9 X100,
U10 X120; U11 Y40, U12 Y60, U13 Y100, U14 Y120, U15 Y140.

Output: Point;Read;Commands;Tag;Wire;Sheet;Relay;RelaySheet;Coil;CoilWire;CoilSheet;Valve;Cylinder;Hoses;Hose;PnSheet;Text;Plc;
        Card;Via;Measure;Pressure
  Hose: the hose this output puts air into - the first output of a valve the
  A hose, the second the B hose (the pneumatic drawing's coil order); a single
  solenoid's coil the A hose, its spring the B one. The step-by-step help draws
  the coil and the cylinder's movement on that side.
  Card: the PLC card and its own terminal name. Via: a sensor read through a
  module (keyed by the relay the program copies it into). Measure / Pressure:
  a cylinder's position measurement and clamping pressure, on its outputs.

Usage:
    python3 tools/build_wiring.py
"""
import io, os, re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
MACHINE = os.path.join(ROOT, 'Kaban.Machines.FA2010')

# ---------------------------------------------------------------- inputs ----
# point, tag, wire, sheet, text (the drawing's own words)
ELECTRICAL_INPUTS = [
    # sheet 11 - FX5U-64MT base unit, encoder inputs
    ('X2',  'Encoder', '70',  '11.1', 'Profile Start Measurement (Gripper) Encoder Input (A Phase)'),
    ('X3',  'Encoder', '71',  '11.1', 'Profile Start Measurement (Gripper) Encoder Input (B Phase)'),
    ('X4',  'Encoder', '76',  '11.2', 'Milling Left Top Clamping Encoder Input (A Phase)'),
    ('X5',  'Encoder', '77',  '11.2', 'Milling Left Top Clamping Encoder Input (B Phase)'),
    ('X6',  'Encoder', '78',  '11.3', 'Cutting Left Top Clamping Encoder Input (A Phase)'),
    ('X7',  'Encoder', '79',  '11.3', 'Cutting Left Top Clamping Encoder Input (B Phase)'),
    ('X10', 'K77',     '80',  '11.4', 'Profile Recognition Unit Width Measurement Encoder Input (A Phase), left or right unit through K77'),
    ('X11', 'K77',     '81',  '11.5', 'Profile Recognition Unit Width Measurement Encoder Input (B Phase), left or right unit through K77'),
    ('X12', 'K78',     '82',  '11.6', 'Profile Recognition Unit Height Measurement Encoder Input (A Phase), left or right unit through K78'),
    ('X13', 'K78',     '83',  '11.7', 'Profile Recognition Unit Height Measurement Encoder Input (B Phase), left or right unit through K78'),
    ('X14', 'Encoder', '84',  '11.8', 'Cutting Right Front Clamping Encoder Input (A Phase)'),
    ('X15', 'Encoder', '85',  '11.8', 'Cutting Right Front Clamping Encoder Input (B Phase)'),
    ('X16', 'Encoder', '86',  '11.9', 'Cutting Right Top Clamping Encoder Input (A Phase)'),
    ('X17', 'Encoder', '87',  '11.9', 'Cutting Right Top Clamping Encoder Input (B Phase)'),
    # sheet 12 - FX5U-64MT base unit
    ('X20', '',        '',    '12.0', 'Safety Relay Input'),
    ('X21', 'SW3',     '90',  '12.1', '6 bar Pressure Input'),
    ('X22', 'SB6/SB4', '91',  '12.1', 'Start Button (Conveyor) / Start Button (Kiosk)'),
    ('X23', 'SN89',    '110', '12.2', 'Left Profile Recognition Unit Back Sensor'),
    ('X24', 'SN38',    '111', '12.2', 'Output Conv. Small Profile Back Sensor'),
    ('X25', 'SN39',    '112', '12.3', 'Output Conv. Small Profile Forward Sensor'),
    ('X26', 'SN90',    '113', '12.3', 'Left Profile Recognition Unit Forward Sensor'),
    ('X34', 'SN46',    '119', '12.6', 'Input Conveyor 6. Clamping Cylinder Back Sensor'),
    ('X35', 'SN47',    '120', '12.7', 'Input Conveyor 7. Clamping Cylinder Back Sensor'),
    # sheet 13 - U7 FX5-16EX/ES (X40 - X57)
    ('X43', 'SN53',    '126', '13.2', 'Input Conveyor 6. Clamping Cylinder Forward Sensor'),
    ('X44', 'SN54',    '127', '13.2', 'Input Conveyor 7. Clamping Cylinder Forward Sensor'),
    ('X45', 'SN22',    '128', '13.3', 'Input Conveyor Profile Detection Reflective Sensor'),
    ('X46', 'SN56',    '129', '13.3', 'Input Robot Gripper Clamping Cylinder Back Sensor'),
    ('X51', 'SN59',    '132', '13.5', 'Profile Recognition Front Clamping Back Sensor'),
    ('X52', 'SN60',    '133', '13.5', 'Profile Recognition Top Clamping Back Sensor'),
    ('X53', 'SN61',    '134', '13.6', 'Milling / Cutting Front Clamping Back Sensor'),
    ('X54', 'SN62',    '135', '13.6', 'Milling Left Top Clamping Up Sensor'),
    ('X56', 'SN64',    '137', '13.7', 'Cutting Left Top Small Clamping Up Sensor'),
    ('X57', 'SN65',    '138', '13.8', 'Cutting Right Top Small Clamping Up Sensor'),
    # sheet 14 - U8 FX5-16EX/ES (X60 - X77)
    ('X60', 'SN76',    '139', '14.0', 'Cutting / Output Front Clamping Back Sensor'),
    ('X61', 'SN77',    '140', '14.1', 'Cutting / Output Top Clamping Up Sensor'),
    ('X63', 'SN79',    '142', '14.2', 'Cutting Foil Roll Back Sensor'),
    ('X64', 'SN80',    '143', '14.2', 'Cutting Foil Roll Forward Sensor'),
    ('X65', 'SN91',    '144', '14.3', 'Cutting Foil Roll Down Sensor'),
    ('X66', 'SN81+SN86', '145', '14.3', 'Output Profile Release (Short/Long) Back Sensor (SN81 and SN86 in series, wire 171 between them)'),
    ('X67', 'SN82',    '146', '14.4', 'Output Profile Release (Short) Forward Sensor'),
    ('X70', 'SN83',    '147', '14.4', 'Profile Recognition Front Clamping Empty Switch'),
    ('X71', 'FT2/FT3', '148', '14.5', 'V Cutting Thermal Trip'),
    ('X72', 'FT1',     '149', '14.5', 'Sawdust Conv. Thermal Trip'),
    ('X73', 'SN84',    '150', '14.6', 'Milling / Cutting Front Clamping Empty Switch'),
    ('X74', 'SN92',    '151', '14.6', 'Output Profile Release Middle Sensor'),
    ('X75', 'SN85',    '152', '14.7', 'Cutting Left Top Clamping Up Sensor'),
    ('X76', 'SN88',    '153', '14.7', 'Output Conv Front Clamping Back Sensor'),
    ('X77', 'SB5',     '154', '14.8', 'Output Conveyor Start Button'),
    # sheet 15 - U9 FX5-16EX/ES (X100 - X117): reference sensors
    ('X100', 'SN.1',  '40', '15.1', 'Input Robot X Axis Reference Sensor'),
    ('X101', 'SN.2',  '42', '15.1', 'Input Robot Y Axis Reference Sensor'),
    ('X102', 'SN.3',  '43', '15.2', 'Input Robot Z Axis Reference Sensor'),
    ('X103', 'SN.4',  '44', '15.2', '1. Milling X Axis Reference Sensor'),
    ('X104', 'SN.5',  '45', '15.3', '1. Milling Y Axis Reference Sensor'),
    ('X105', 'SN.6',  '46', '15.3', '1. Milling Z Axis Reference Sensor'),
    ('X106', 'SN.7',  '47', '15.4', '1. Milling Angle Axis Reference Sensor'),
    ('X107', 'SN.8',  '48', '15.4', '2. Milling X Axis Reference Sensor'),
    ('X110', 'SN.9',  '49', '15.5', '2. Milling Y Axis Reference Sensor'),
    ('X111', 'SN.10', '50', '15.5', '2. Milling Z Axis Reference Sensor'),
    ('X112', 'SN.11', '51', '15.6', '2. Milling Angle Axis Reference Sensor'),
    ('X113', 'SN.12', '52', '15.6', 'Cutting A Axis Reference Sensor'),
    ('X114', 'SN.13', '53', '15.7', 'Cutting Z Axis Reference Sensor'),
    ('X115', 'SN.14', '55', '15.7', 'V Cutting / Mullion Milling Y Axis Reference Sensor'),
    ('X116', 'SN.17', '56', '15.8', 'Exit Robot Reference Sensor'),
    ('X117', 'SN.18', '58', '15.8', 'V Cutting / Mullion Milling Z Axis Reference Sensor'),
    # sheet 16 - U10 FX5-16EX/ES (X120 - X137)
    ('X120', 'SN.97',  '59',  '16.1', 'Output Conveyor Full Sensor'),
    ('X121', 'SN98',   '60',  '16.1', 'Output Profile Release (Long) Forward Sensor'),
    ('X122', 'SN95',   '185', '16.2', 'Input Robot Gripper Angle 0 Cylinder Sensor'),
    ('X123', 'SN96',   '186', '16.2', 'Input Robot Gripper Angle 90 Cylinder Sensor'),
    ('X124', 'SN.99',  '187', '16.3', 'Left Profile Recognition Profile Cleared Laser Sensor'),
    ('X126', 'SN.101', '189', '16.4', '1. Cross Roller Back Sensor'),
    ('X127', 'SN.102', '190', '16.4', '2. Cross Roller Back Sensor'),
    ('X130', 'SN.103', '191', '16.5', '3. Cross Roller Back Sensor'),
    ('X131', 'SN.104', '192', '16.5', 'Mullion Milling X Axis Reference Sensor'),
    ('X132', 'SN105',  '193', '16.6', 'Mullion Milling Unit Up Sensor'),
    ('X133', 'SN106',  '194', '16.6', 'Mullion Milling Unit Down Sensor'),
    ('X134', 'SN.107', '195', '16.7', 'Input Conveyor Position Laser Sensor'),
    ('X135', 'SN108',  '196', '16.7', 'Mullion Milling Blade Change Down Sensor'),
    ('X136', 'SN112',  '197', '16.8', 'Milling / Cutting Front Clamping Forward Switch'),
]

# --------------------------------------------------------------- outputs ----
# point, relay, wire (PLC side), relay sheet, coil (valve solenoid or load),
# coil wire, coil sheet (where the relay's contact switches it), text
ELECTRICAL_OUTPUTS = [
    # sheet 18 - FX5U-64MT base unit, contacts on sheet 25
    ('Y0',   'K5',  '200', '18.1', 'H4',   '300', '25.0', 'Horn (and yellow lamp H3)'),
    ('Y1',   '',    '201', '18.1', '',     '',    '7.7',  'Input Conveyor Forward (inverter)'),
    ('Y2',   '',    '202', '18.2', '',     '',    '7.7',  'Input Conveyor Back (inverter)'),
    ('Y3',   'K6',  '203', '18.2', 'Y.81', '301', '25.1', 'Output Conveyor Small Profile Forward Valve'),
    ('Y4',   'K7',  '204', '18.3', 'Y.82', '302', '25.2', 'Output Conveyor Small Profile Back Valve'),
    ('Y12',  'K13', '210', '18.6', 'Y.7',  '308', '25.5', 'Input Conveyor 6. Clamping Cylinder Clamp Valve'),
    ('Y13',  'K14', '211', '18.6', 'Y.8',  '309', '25.6', 'Input Conveyor 7. Clamping Cylinder Clamp Valve'),
    # sheet 19 - contacts on sheets 26, 27
    ('Y21',  'K20', '217', '19.1', 'Y.14', '315', '26.1', 'Input Conveyor 6. Clamping Cylinder Release Valve'),
    ('Y22',  'K21', '218', '19.2', 'Y.72', '316', '26.2', 'Input Conveyor 7. Clamping Cylinder Release Valve'),
    ('Y23',  'K22', '219', '19.2', 'Y.16', '317', '26.2', 'Input Robot Gripper Clamping Cylinder Clamp Valve'),
    ('Y24',  'K23', '220', '19.3', 'Y.17', '318', '26.3', 'Input Robot Gripper Clamping Cylinder Release Valve'),
    ('Y25',  'K24', '221', '19.3', 'Y1.6', '319', '26.3', 'Input Robot Z Axis Brake'),
    ('Y32',  'K29', '226', '19.6', 'Y.23', '324', '26.7', 'Profile Recognition Front Clamping Clamp Valve'),
    ('Y33',  'K30', '227', '19.6', 'Y.24', '325', '26.7', 'Profile Recognition Front Clamping Release Valve'),
    ('Y34',  'K31', '228', '19.7', 'Y.25', '326', '26.8', 'Profile Recognition Top Clamping Clamp Valve'),
    ('Y35',  'K32', '229', '19.7', 'Y.26', '327', '26.8', 'Profile Recognition Top Clamping Release Valve'),
    ('Y36',  'K33', '230', '19.8', 'Y29',  '328', '27.0', '1. Milling Z Axis Brake'),
    ('Y37',  'K34', '231', '19.8', 'Y.30', '329', '27.1', '2. Milling Z Axis Brake'),
    # sheet 20 - U11 FX5-16EYT-ESS (Y40 - Y57), contacts on sheets 27, 30
    ('Y40',  '',    '232', '20.1', '',     '',    '8.2',  '1. Milling Inverter Run'),
    ('Y41',  '',    '233', '20.1', '',     '',    '8.7',  '2. Milling Inverter Run'),
    ('Y42',  'K2',  '234', '20.2', 'Y.98', '377', '30.3', 'Input Conveyor Rolls Down Valve'),
    ('Y43',  'K35', '235', '20.2', 'Y.31', '330', '27.2', 'Cutting Z Axis Brake'),
    ('Y44',  'K36', '236', '20.3', 'Y.36', '331', '27.2', 'Milling / Cutting Front Clamping Forward Valve'),
    ('Y45',  'K37', '237', '20.3', 'Y.37', '332', '27.3', 'Milling / Cutting Front Clamping Back Valve'),
    ('Y46',  'K38', '238', '20.4', 'Y.38', '333', '27.3', 'Milling Left Top Clamping Down Valve (with Y.103, Milling Left Top Sawdust Cleaning Valve)'),
    ('Y47',  'K39', '239', '20.4', 'Y.39', '334', '27.4', 'Milling Left Top Clamping Up Valve'),
    ('Y52',  'K42', '242', '20.6', 'Y.68', '337', '27.6', 'Cutting Left Top Clamping Down Valve (with Y.104, Cutting Left Top Sawdust Cleaning Valve)'),
    ('Y53',  'K43', '243', '20.6', 'Y.69', '338', '27.7', 'Cutting Left Top Clamping Up Valve'),
    ('Y54',  'K44', '244', '20.7', 'Y.70', '339', '27.7', 'Cutting / Output Front Clamping Forward Valve'),
    ('Y55',  'K45', '245', '20.7', 'Y.71', '340', '27.8', 'Cutting / Output Front Clamping Back Valve'),
    ('Y56',  'K46', '246', '20.8', 'Y.40', '341', '27.8', 'Cutting / Output Top Clamping Down Valve (with Y.105, Cutting / Exit Top Sawdust Cleaning Valve)'),
    ('Y57',  'K47', '247', '20.8', 'Y.41', '342', '27.9', 'Cutting / Output Top Clamping Up Valve'),
    # sheet 21 - U12 (Y60 - Y77), contacts on sheets 28, 30
    ('Y62',  'K50', '249', '21.2', 'Y.106', '345', '28.2', 'Output Profile Release (Long) Forward Valve'),
    ('Y63',  '',    '251', '21.2', '',      '',    '37.7', 'V Cutting Inverter Run'),
    ('Y64',  'K52', '252', '21.3', 'Y.46',  '347', '28.3', 'Lubrication Valve'),
    ('Y65',  'K53', '253', '21.3', 'Y.47',  '348', '28.3', 'Output Profile Release (Short) Forward Valve'),
    ('Y66',  '',    '254', '21.4', '',      '',    '9.5',  'Output Conveyor Inverter Run'),
    ('Y67',  'K54', '255', '21.4', 'Y.48',  '349', '28.4', 'Profile Recognition Unit Low/High Pressure Selection Valve'),
    ('Y70',  'K55', '256', '21.5', 'Y.49',  '350', '28.5', 'Milling Unit Low/High Pressure Selection Valve'),
    ('Y71',  'K56', '257', '21.5', 'Y.50',  '351', '28.5', 'Cutting Unit Low/High Pressure Selection Valve'),
    ('Y72',  'K57', '258', '21.6', 'Y.51',  '352', '28.6', 'Profile Recognition / Milling / Cutting Table Sawdust Cleaning Valve (with Y.80)'),
    ('Y73',  'K58', '259', '21.6', 'Y.79',  '353', '28.7', 'Cutting Leakage Throw Valve (with Y.52)'),
    ('Y74',  'K4',  '260', '21.7', 'Y.99',  '378', '30.4', 'Input Conveyor Rolls Up Valve'),
    ('Y75',  'K59', '261', '21.7', 'Y.78',  '354', '28.8', 'Input Robot X Axis Rack Cleaning Valve'),
    ('Y76',  '',    '262', '21.8', '',      '',    '7.2',  'Saw Inverter Run'),
    ('Y77',  'K61', '263', '21.8', 'Y.55',  '356', '28.8', 'Milling 1-1 Tool Cooling Valve'),
    # sheet 22 - U13 (Y100 - Y117), contacts on sheets 28, 29
    ('Y100', 'K62', '264', '22.1', 'Y.56',  '357', '28.9', 'Milling 1-2 Tool Cooling Valve'),
    ('Y101', 'K63', '265', '22.1', 'Y.73',  '358', '29.0', 'Milling 2-1 Tool Cooling Valve'),
    ('Y102', 'K64', '266', '22.2', 'Y.74',  '359', '29.1', 'Milling 2-2 Tool Cooling Valve'),
    ('Y103', 'K65', '267', '22.2', 'Y.75',  '360', '29.1', 'Saw Cooling Valve'),
    ('Y104', 'K66', '268', '22.3', 'Y.76',  '361', '29.2', 'V Cutting / Mullion Milling Z Axis Brake'),
    ('Y105', 'K67', '269', '22.3', 'Y.83',  '362', '29.3', 'Output Conv. Front Clamping Clamp Valve'),
    ('Y106', 'K68', '270', '22.4', 'Y.84',  '363', '29.3', 'Output Conv. Front Clamping Release Valve'),
    ('Y107', 'K60', '271', '22.4', 'K3',    '355', '28.8', 'Sawdust Conveyor Contactor'),
    ('Y110', 'K69', '272', '22.5', 'Y.85',  '364', '29.4', 'Cutting Left Top Small Clamping Forward Valve'),
    ('Y111', 'K70', '273', '22.5', 'Y.86',  '365', '29.5', 'Cutting Left Top Small Clamping Back Valve'),
    ('Y112', 'K71', '274', '22.6', 'Y.87',  '366', '29.5', 'Cutting Right Top Small Clamping Forward Valve'),
    ('Y113', 'K72', '275', '22.6', 'Y.88',  '367', '29.6', 'Cutting Right Top Small Clamping Back Valve'),
    ('Y114', 'K73', '276', '22.7', 'Y.89',  '368', '29.7', 'Cutting Foil Roll Forward Valve'),
    ('Y115', 'K74', '277', '22.7', 'Y.90',  '369', '29.7', 'Cutting Foil Roll Back Valve'),
    ('Y116', 'K75', '278', '22.8', 'Y.91',  '370', '29.8', 'Cutting Foil Roll Up Valve (with Y.92, Cutting Foil Sawdust Cleaning Valve)'),
    ('Y117', 'K76', '279', '22.8', 'Y.93',  '371', '29.9', 'Output Sawdust Cleaning Valve'),
    # sheet 23 - U14 (Y120 - Y137), contacts on sheets 11, 30, 31
    ('Y120', 'K77/K78', '280', '23.1', '', '', '11.4', 'Left Profile Recognition Unit Width / Height Measurement Encoder Selection'),
    ('Y121', 'K79', '281', '23.2', 'Y.101', '372', '30.0', 'Left Profile Recognition Unit Forward Valve'),
    ('Y122', 'K80', '282', '23.2', 'Y.102', '373', '30.1', 'Left Profile Recognition Unit Back Valve'),
    ('Y123', 'K81', '283', '23.3', 'Y.96',  '374', '30.1', 'Left Profile Recognition Unit Down Valve'),
    ('Y124', 'K82', '284', '23.3', 'Y.97',  '375', '30.2', 'Left Profile Recognition Unit Up Valve'),
    ('Y125', 'K83', '285', '23.4', 'Y.100', '376', '30.3', 'Saw Sawdust Cleaning Valve'),
    ('Y126', 'K84', '286', '23.4', 'Y.107', '379', '30.5', 'Input Robot Lubrication Valve'),
    ('Y127', 'K85', '287', '23.5', 'Y.108', '380', '30.5', 'Input Robot Gripper Angle 0 Cylinder Valve'),
    ('Y130', 'K86', '288', '23.5', 'Y.109', '381', '30.6', 'Input Robot Gripper Angle 90 Cylinder Valve'),
    ('Y131', 'K87', '289', '23.6', 'Y.110', '382', '30.6', 'Left Profile Recognition Front Mold Up Valve'),
    ('Y132', 'K88', '290', '23.6', 'Y.111', '383', '30.7', 'Left Profile Recognition Front Mold Down Valve'),
    ('Y133', 'K89', '291', '23.7', 'Y.112', '600', '31.0', 'Input Conveyor Front Mold Up Valve'),
    ('Y134', 'K90', '292', '23.7', 'Y.113', '601', '31.1', 'Input Conveyor Front Mold Down Valve'),
    ('Y135', 'K91', '293', '23.8', 'Y.114', '602', '31.2', 'Profile Recognition Front Mold Up Valve'),
    ('Y136', 'K92', '294', '23.8', 'Y.115', '603', '31.2', 'Profile Recognition Front Mold Down Valve'),
    ('Y137', 'K93', '295', '23.9', 'Y.116', '604', '31.3', 'Milling / Cutting Front Mold Up Valve'),
    # sheet 24 - U15 (Y140 - Y157), contacts on sheets 31, 32
    ('Y140', 'K94',  '500', '24.1', 'Y.147', '605', '31.3', 'Milling / Cutting Front Mold Down Valve'),
    ('Y141', 'K95',  '501', '24.2', 'Y.117', '606', '31.4', 'Cutting / Output Front Mold Up Valve'),
    ('Y142', 'K96',  '502', '24.2', 'Y.118', '607', '31.5', 'Cutting / Output Front Mold Down Valve'),
    ('Y143', 'K97',  '503', '24.3', 'Y.119', '608', '31.5', '1. Cross Roller Down Valve'),
    ('Y144', 'K98',  '504', '24.3', 'Y.120', '609', '31.6', '1. Cross Roller Up Valve'),
    ('Y145', 'K99',  '505', '24.4', 'Y.121', '610', '31.7', '2. Cross Roller Down Valve'),
    ('Y146', 'K100', '506', '24.4', 'Y.122', '611', '31.7', '2. Cross Roller Up Valve'),
    ('Y147', 'K101', '507', '24.5', 'Y.123', '612', '31.8', '3. Cross Roller Down Valve'),
    ('Y150', 'K102', '508', '24.5', 'Y.124', '613', '31.8', '3. Cross Roller Up Valve'),
    ('Y151', 'K103', '509', '24.6', 'Y.125', '614', '32.0', 'Output Conveyor Front Mold Up Valve'),
    ('Y152', 'K104', '510', '24.6', 'Y.126', '615', '32.1', 'Output Conveyor Front Mold Down Valve'),
    ('Y153', 'K105', '511', '24.7', 'Y.127', '616', '32.2', 'Mullion Milling Unit Up Valve'),
    ('Y154', 'K106', '512', '24.7', 'Y.128', '617', '32.2', 'Mullion Milling Unit Down Valve'),
    ('Y155', 'K107', '513', '24.8', 'Y.129', '618', '32.3', 'Mullion Milling Blade Change Valve'),
]

# ------------------------------------------------------------- pneumatic ----
# outputs, valve (the pneumatic drawing's own coil names), cylinder, hoses
# (A and B port), sheet and zone. Matched to the electrical drawing by what
# the two call the movement; the pneumatic drawing numbers its valves apart
# (Y20/Y21 there is Y.7/Y.14 on the electrical one).
PNEUMATIC = [
    (('Y3', 'Y4'),     'Y71/Y72 (1/8 5/2 double solenoid)', 'P36 Output Conveyor Small Profile Cylinder', '83 (A) / 84 (B)', '5 D1'),
    (('Y12', 'Y21'),   'Y20/Y21 (1/8 5/2 double solenoid)', 'P12 Input Conveyor 6. Clamping Cylinder', '16 (A) / 96 (B)', '2 E3'),
    (('Y13', 'Y22'),   'Y22/Y23 (1/8 5/2 double solenoid)', 'P13 Input Conveyor 7. Clamping Cylinder', '17 (A) / 97 (B)', '2 E2'),
    (('Y23', 'Y24'),   'Y24/Y25 (1/8 5/2 double solenoid)', 'P14 Input Robot Gripper Clamping Cylinder', '18 (A) / 19 (B)', '2 E1'),
    (('Y32', 'Y33'),   'Y28/Y29 (1/8 5/3 open centre)', 'P16 Profile Recognition Front Clamping Cylinder (pressure switch SW1)', '23 (A) / 24 (B)', '3 E8'),
    (('Y34', 'Y35'),   'Y31/Y32 (1/8 5/3 closed centre)', 'P17 Profile Recognition Top Clamping Cylinder', '25 (A) / 26 (B)', '3 E7'),
    (('Y46', 'Y47'),   'Y33/Y34 (1/8 5/3 closed centre)', 'P18 Milling Top Clamping Cylinder', '27 (A) / 28 (B)', '3 E6'),
    (('Y44', 'Y45'),   'Y35/Y36 (1/8 5/3 open centre)', 'P19 Milling / Cutting Front Clamping Cylinder (pressure switches SW2, SW3)', '30 (A) / 31 (B)', '3 E5'),
    (('Y52', 'Y53'),   'Y38/Y39 (1/8 5/3 closed centre)', 'P20 Cutting Left Top Clamping Cylinder', '37 (A) / 38 (B)', '3 E4'),
    (('Y50', 'Y51'),   'Y40/Y41 (1/8 5/2 double solenoid)', 'P21 Milling Roll Cylinder', '32 (A) / 33 (B)', '3 E3'),
    (('Y54', 'Y55'),   'Y43/Y44 (1/8 5/3 closed centre)', 'P22 Cutting / Output Front Clamping Cylinder', '35 (A) / 36 (B)', '3 E2'),
    (('Y56', 'Y57'),   'Y45/Y46 (1/8 5/3 closed centre)', 'P23 Cutting / Output Top Clamping Cylinder', '39 (A) / 40 (B)', '3 E1'),
    (('Y67',),         'Y30 (1/8 5/2 single solenoid)', 'clamping pressure for P16 and P17 (6 / 2 bar)', '22', '3 B7'),
    (('Y70',),         'Y37 (1/8 5/2 single solenoid)', 'clamping pressure for P18, P19 and P20 (6 / 2 bar)', '29', '3 B4'),
    (('Y71',),         'Y42 (1/8 5/2 single solenoid)', 'clamping pressure for P22 and P23 (6 / 2 bar)', '34', '3 B1'),
    (('Y73',),         'Y2 (1/8 5/2 single solenoid)', 'Cutting Leakage Throw', '60', '1 C6'),
    (('Y105', 'Y106'), 'Y48/Y49 (1/8 5/2 double solenoid)', 'P24 Output Conveyor Front Clamping Cylinder', '41 (A) / 42 (B)', '4 D9'),
    (('Y65',),         'Y47 (1/8 5/2 single solenoid)', 'P25 Output Profile Release (Short) Cylinder', '43 (A) / 44 (B)', '4 D8'),
    (('Y62',),         'Y50 (1/8 5/2 single solenoid)', 'P26 and P27 Output Profile Release (Long) Cylinders', '45 (A) / 46 (B)', '4 D7'),
    (('Y110', 'Y111'), 'Y3/Y4 (1/8 5/2 double solenoid)', 'P1 Cutting Left Top Small Clamping Cylinder', '61 (A) / 62 (B)', '1 D5'),
    (('Y112', 'Y113'), 'Y5/Y6 (1/8 5/2 double solenoid)', 'P2 Cutting Right Top Small Clamping Cylinder', '63 (A) / 64 (B)', '1 D4'),
    (('Y114', 'Y115'), 'Y59/Y60 (1/8 5/2 double solenoid)', 'P28 Cutting Foil Roll Forward-Back Cylinder', '70 (A) / 71 (B)', '5 D9'),
    (('Y116',),        'Y61 (1/8 5/2 single solenoid)', 'P29 Cutting Foil Roll Up-Down Cylinder', '72 (A) / 73 (B)', '5 D8'),
    (('Y123', 'Y124'), 'Y65/Y66 (1/8 5/2 double solenoid)', 'P30 Left Profile Recognition Unit Up / Down Cylinder', '76 (A) / 77 (B)', '5 D4'),
    (('Y121', 'Y122'), 'Y67/Y68 (1/8 5/2 double solenoid)', 'P31 Left Profile Recognition Unit Forward/Back Cylinder', '78 (A) / 79 (B)', '5 D3'),
    (('Y42', 'Y74'),   'Y69/Y70 (1/8 5/2 double solenoid)', 'P32 - P35 Entry Conveyor Roll Cylinders 1 - 4', '80 (A) / 81 (B)', '5 C2'),
    (('Y131', 'Y132'), 'Y73/Y74 (1/8 5/3 open centre)', 'P37 Left Profile Recognition Front Up-Down Mold Cylinder', '101 (A) / 102 (B)', '6 D9'),
    (('Y133', 'Y134'), 'Y79/Y80 (1/8 5/3 open centre)', 'P40 Input Conveyor Front Up-Down Mold Cylinder', '108 (A) / 109 (B)', '6 D4'),
    (('Y135', 'Y136'), 'Y85/Y86 (1/8 5/3 open centre)', 'P40 Profile Recognition Front Up-Down Mold Cylinder', '115 (A) / 116 (B)', '7 D9'),
    (('Y137', 'Y140'), 'Y91/Y92 (1/8 5/3 open centre)', 'P40 Milling / Cutting Front Up-Down Mold Cylinder', '122 (A) / 123 (B)', '7 D4'),
    (('Y141', 'Y142'), 'Y97/Y98 (1/8 5/3 open centre)', 'P40 Cutting / Output Front Up-Down Mold Cylinder', '129 (A) / 130 (B)', '8 D9'),
    (('Y151', 'Y152'), 'Y120/Y121 (1/8 5/3 open centre)', 'P57 Output Front Up-Down Mold Cylinder', '160 (A) / 161 (B)', '8 D4'),
    (('Y153', 'Y154'), 'Y122/Y123 (1/8 5/2 double solenoid)', 'P58 Mullion Milling Unit Cylinder', '162 (A) / 163 (B)', '8 D3'),
    (('Y155',),        'Y124 (1/8 5/2 single solenoid)', 'P60 Blade Change Cylinder', '164 (A) / 165 (B)', '8 D1'),
    (('Y143', 'Y144'), 'Y107/Y108 (1/8 5/2 double solenoid)', 'P54 1. Cross Roller Cylinder', '140 (A) / 141 (B)', '9 E9'),
    (('Y145', 'Y146'), 'Y109/Y110 (1/8 5/2 double solenoid)', 'P55 2. Cross Roller Cylinder', '142 (A) / 143 (B)', '9 E8'),
    (('Y147', 'Y150'), 'Y111/Y112 (1/8 5/2 double solenoid)', 'P56 3. Cross Roller Cylinder', '144 (A) / 145 (B)', '9 E7'),
    (('Y127', 'Y130'), 'Y117/Y118 (1/8 5/2 double solenoid)', 'P62 Gripper Angle Cylinder', '154 (A) / 155 (B)', '9 E3'),
]

# ------------------------------------------------ read through a module ----
# Sensors the drawing wires to the simple motion modules' DI terminals (sheet
# 17), not to an X input. The PLC program copies them into relays - the ones
# its manual screens use as those cylinders' sensors (the device list's
# ManualMovement sheet: Left Top Clamping M345, Roller Up/Down M346): keyed by
# that relay. point (relay), tag, wire, sheet, via, text
MODULE_INPUTS = [
    ('M345', 'SN93', '175', '17.7', 'DI1 of the 2nd simple motion module (2/FX5-80SSC-S)',
     'Left Profile Recognition Unit Up Sensor'),
    ('M346', '2SN.94 / 2SN.95', '173 / 174', '17.6 - 17.7', 'DI2 / DI3 of the 2nd simple motion module (2/FX5-80SSC-S)',
     'Input Conveyor Roll Down Sensors (left / right)'),
]

# ------------------------------------------------------ what a cylinder has --
# Beside its valve: how its position is measured, and the pressure it clamps
# with. Keyed, like PNEUMATIC, by the outputs that drive it.
MEASURE = [
    (('Y23', 'Y24'), 'encoder X2/X3 (wires 70/71, sheet 11.1) - Profile Start Measurement (Gripper) encoder'),
    (('Y46', 'Y47'), 'encoder X4/X5 (wires 76/77, sheet 11.2) - Milling Left Top Clamping encoder'),
    (('Y52', 'Y53'), 'encoder X6/X7 (wires 78/79, sheet 11.3) - Cutting Left Top Clamping encoder'),
    (('Y54', 'Y55'), 'encoder X14/X15 (wires 84/85, sheet 11.8) - Cutting Right Front Clamping encoder'),
    (('Y56', 'Y57'), 'encoder X16/X17 (wires 86/87, sheet 11.9) - Cutting Right Top Clamping encoder'),
    (('Y32', 'Y33'), 'width encoder (wires 102/103) through relay K77 to X10/X11 (wires 80/81, sheet 11.5); '
                     'Y120 switches K77 to the left unit'),
    (('Y34', 'Y35'), 'height encoder (wires 106/107) through relay K78 to X12/X13 (wires 82/83, sheet 11.7); '
                     'Y120 switches K78 to the left unit'),
    (('Y121', 'Y122'), 'width encoder of the left unit (wires 100/101) through relay K77 to X10/X11 (wires 80/81, sheet 11.4), '
                       'while Y120 selects the left unit'),
    (('Y123', 'Y124'), 'height encoder of the left unit (wires 104/105) through relay K78 to X12/X13 (wires 82/83, sheet 11.6), '
                       'while Y120 selects the left unit'),
    (('Y131', 'Y132'), 'analogue sensor SN110 (SDAT-MHS-M100, 4-20 mA), wire 700 to U19 FX5-8AD channel 1 (sheet 34.4)'),
    (('Y133', 'Y134'), 'analogue sensor SN111 (SDAT-MHS-M100, 4-20 mA), wire 701 to U19 FX5-8AD channel 2 (sheet 34.5)'),
    (('Y135', 'Y136'), 'analogue sensor SN112 (SDAT-MHS-M50, 4-20 mA), wire 702 to U19 FX5-8AD channel 3 (sheet 34.7)'),
    (('Y137', 'Y140'), 'analogue sensor SN113 (SDAT-MHS-M100, 4-20 mA), wire 703 to U19 FX5-8AD channel 4 (sheet 34.8)'),
    (('Y141', 'Y142'), 'analogue sensor SN114 (SDAT-MHS-M100, 4-20 mA), wire 704 to U19 FX5-8AD channel 5 (sheet 34.4)'),
    (('Y151', 'Y152'), 'analogue sensor SN115 (SDAT-MHS-M100, 4-20 mA), wire 705 to U19 FX5-8AD channel 6 (sheet 34.5)'),
]
PRESSURE = [
    (('Y32', 'Y33'), 'clamping pressure 6 / 2 bar: valve Y30 (hose 22, pneumatic sheet 3 B7) switched by Y67 - relay K54 (sheet 21.4); '
                     'pressure switch SW1 (0.2 bar, pneumatic sheet 3 F8) = SN83 on X70 (wire 147, sheet 14.4)'),
    (('Y34', 'Y35'), 'clamping pressure 6 / 2 bar: valve Y30 (hose 22, pneumatic sheet 3 B7) switched by Y67 - relay K54 (sheet 21.4)'),
    (('Y46', 'Y47'), 'clamping pressure 6 / 2 bar: valve Y37 (hose 29, pneumatic sheet 3 B4) switched by Y70 - relay K55 (sheet 21.5)'),
    (('Y44', 'Y45'), 'clamping pressure 6 / 2 bar: valve Y37 (hose 29, pneumatic sheet 3 B4) switched by Y70 - relay K55 (sheet 21.5); '
                     'pressure switches SW2 / SW3 (0.2 bar, pneumatic sheet 3 F5) - on the electrical drawing SN84 on X73 '
                     '(wire 150, sheet 14.6) and SN112 on X136 (wire 197, sheet 16.8)'),
    (('Y52', 'Y53'), 'clamping pressure 6 / 2 bar: valve Y37 (hose 29, pneumatic sheet 3 B4) switched by Y70 - relay K55 (sheet 21.5)'),
    (('Y54', 'Y55'), 'clamping pressure 6 / 2 bar: valve Y42 (hose 34, pneumatic sheet 3 B1) switched by Y71 - relay K56 (sheet 21.5)'),
    (('Y56', 'Y57'), 'clamping pressure 6 / 2 bar: valve Y42 (hose 34, pneumatic sheet 3 B1) switched by Y71 - relay K56 (sheet 21.5)'),
]

# ------------------------------------------------- the same texts in Turkish --
# The step-by-step help reads a text in the HMI's language from a column of its
# own ("Measure.TR"); the drawings' own names (the encoder's label) stay as drawn.
TRANSLATED = {
    'TR': {
        'encoder X2/X3 (wires 70/71, sheet 11.1) - Profile Start Measurement (Gripper) encoder':
            'enkoder X2/X3 (kablolar 70/71, sayfa 11.1) - çizimde: Profile Start Measurement (Gripper) encoder',
        'encoder X4/X5 (wires 76/77, sheet 11.2) - Milling Left Top Clamping encoder':
            'enkoder X4/X5 (kablolar 76/77, sayfa 11.2) - çizimde: Milling Left Top Clamping encoder',
        'encoder X6/X7 (wires 78/79, sheet 11.3) - Cutting Left Top Clamping encoder':
            'enkoder X6/X7 (kablolar 78/79, sayfa 11.3) - çizimde: Cutting Left Top Clamping encoder',
        'encoder X14/X15 (wires 84/85, sheet 11.8) - Cutting Right Front Clamping encoder':
            'enkoder X14/X15 (kablolar 84/85, sayfa 11.8) - çizimde: Cutting Right Front Clamping encoder',
        'encoder X16/X17 (wires 86/87, sheet 11.9) - Cutting Right Top Clamping encoder':
            'enkoder X16/X17 (kablolar 86/87, sayfa 11.9) - çizimde: Cutting Right Top Clamping encoder',
        'width encoder (wires 102/103) through relay K77 to X10/X11 (wires 80/81, sheet 11.5); Y120 switches K77 to the left unit':
            'genişlik enkoderi (kablolar 102/103), K77 rölesi üzerinden X10/X11 girişlerine (kablolar 80/81, sayfa 11.5); '
            'Y120, K77 rölesini sol üniteye geçirir',
        'height encoder (wires 106/107) through relay K78 to X12/X13 (wires 82/83, sheet 11.7); Y120 switches K78 to the left unit':
            'yükseklik enkoderi (kablolar 106/107), K78 rölesi üzerinden X12/X13 girişlerine (kablolar 82/83, sayfa 11.7); '
            'Y120, K78 rölesini sol üniteye geçirir',
        'width encoder of the left unit (wires 100/101) through relay K77 to X10/X11 (wires 80/81, sheet 11.4), while Y120 selects the left unit':
            'sol ünitenin genişlik enkoderi (kablolar 100/101), K77 rölesi üzerinden X10/X11 girişlerine (kablolar 80/81, sayfa 11.4); '
            'Y120 sol üniteyi seçtiği sürece',
        'height encoder of the left unit (wires 104/105) through relay K78 to X12/X13 (wires 82/83, sheet 11.6), while Y120 selects the left unit':
            'sol ünitenin yükseklik enkoderi (kablolar 104/105), K78 rölesi üzerinden X12/X13 girişlerine (kablolar 82/83, sayfa 11.6); '
            'Y120 sol üniteyi seçtiği sürece',
        'analogue sensor SN110 (SDAT-MHS-M100, 4-20 mA), wire 700 to U19 FX5-8AD channel 1 (sheet 34.4)':
            'analog sensör SN110 (SDAT-MHS-M100, 4-20 mA), kablo 700 ile U19 FX5-8AD kanal 1 (sayfa 34.4)',
        'analogue sensor SN111 (SDAT-MHS-M100, 4-20 mA), wire 701 to U19 FX5-8AD channel 2 (sheet 34.5)':
            'analog sensör SN111 (SDAT-MHS-M100, 4-20 mA), kablo 701 ile U19 FX5-8AD kanal 2 (sayfa 34.5)',
        'analogue sensor SN112 (SDAT-MHS-M50, 4-20 mA), wire 702 to U19 FX5-8AD channel 3 (sheet 34.7)':
            'analog sensör SN112 (SDAT-MHS-M50, 4-20 mA), kablo 702 ile U19 FX5-8AD kanal 3 (sayfa 34.7)',
        'analogue sensor SN113 (SDAT-MHS-M100, 4-20 mA), wire 703 to U19 FX5-8AD channel 4 (sheet 34.8)':
            'analog sensör SN113 (SDAT-MHS-M100, 4-20 mA), kablo 703 ile U19 FX5-8AD kanal 4 (sayfa 34.8)',
        'analogue sensor SN114 (SDAT-MHS-M100, 4-20 mA), wire 704 to U19 FX5-8AD channel 5 (sheet 34.4)':
            'analog sensör SN114 (SDAT-MHS-M100, 4-20 mA), kablo 704 ile U19 FX5-8AD kanal 5 (sayfa 34.4)',
        'analogue sensor SN115 (SDAT-MHS-M100, 4-20 mA), wire 705 to U19 FX5-8AD channel 6 (sheet 34.5)':
            'analog sensör SN115 (SDAT-MHS-M100, 4-20 mA), kablo 705 ile U19 FX5-8AD kanal 6 (sayfa 34.5)',
        'clamping pressure 6 / 2 bar: valve Y30 (hose 22, pneumatic sheet 3 B7) switched by Y67 - relay K54 (sheet 21.4); '
        'pressure switch SW1 (0.2 bar, pneumatic sheet 3 F8) = SN83 on X70 (wire 147, sheet 14.4)':
            'sıkma basıncı 6 / 2 bar: Y30 valfi (hortum 22, pnömatik sayfa 3 B7), Y67 ile K54 rölesi (sayfa 21.4) üzerinden çevrilir; '
            'SW1 basınç anahtarı (0,2 bar, pnömatik sayfa 3 F8) = X70 girişindeki SN83 (kablo 147, sayfa 14.4)',
        'clamping pressure 6 / 2 bar: valve Y30 (hose 22, pneumatic sheet 3 B7) switched by Y67 - relay K54 (sheet 21.4)':
            'sıkma basıncı 6 / 2 bar: Y30 valfi (hortum 22, pnömatik sayfa 3 B7), Y67 ile K54 rölesi (sayfa 21.4) üzerinden çevrilir',
        'clamping pressure 6 / 2 bar: valve Y37 (hose 29, pneumatic sheet 3 B4) switched by Y70 - relay K55 (sheet 21.5)':
            'sıkma basıncı 6 / 2 bar: Y37 valfi (hortum 29, pnömatik sayfa 3 B4), Y70 ile K55 rölesi (sayfa 21.5) üzerinden çevrilir',
        'clamping pressure 6 / 2 bar: valve Y37 (hose 29, pneumatic sheet 3 B4) switched by Y70 - relay K55 (sheet 21.5); '
        'pressure switches SW2 / SW3 (0.2 bar, pneumatic sheet 3 F5) - on the electrical drawing SN84 on X73 '
        '(wire 150, sheet 14.6) and SN112 on X136 (wire 197, sheet 16.8)':
            'sıkma basıncı 6 / 2 bar: Y37 valfi (hortum 29, pnömatik sayfa 3 B4), Y70 ile K55 rölesi (sayfa 21.5) üzerinden çevrilir; '
            'SW2 / SW3 basınç anahtarları (0,2 bar, pnömatik sayfa 3 F5) - elektrik çiziminde X73 girişindeki SN84 '
            '(kablo 150, sayfa 14.6) ve X136 girişindeki SN112 (kablo 197, sayfa 16.8)',
        'clamping pressure 6 / 2 bar: valve Y42 (hose 34, pneumatic sheet 3 B1) switched by Y71 - relay K56 (sheet 21.5)':
            'sıkma basıncı 6 / 2 bar: Y42 valfi (hortum 34, pnömatik sayfa 3 B1), Y71 ile K56 rölesi (sayfa 21.5) üzerinden çevrilir',
        'DI1 of the 2nd simple motion module (2/FX5-80SSC-S)':
            '2. simple motion modülünün DI1 girişi (2/FX5-80SSC-S)',
        'DI2 / DI3 of the 2nd simple motion module (2/FX5-80SSC-S)':
            '2. simple motion modülünün DI2 / DI3 girişleri (2/FX5-80SSC-S)',
    }
}
TRANSLATED_COLUMNS = ['Measure', 'Pressure', 'Via']

# ------------------------------------------------------ the PLC's cards ----
# Sheet 36: the base unit, then the extension modules in their order on the bus.
CARDS = [
    ('X', 0o0,   0o37,  'U4 FX5U-64MT-ESS'),
    ('X', 0o40,  0o57,  'U7 FX5-16EX/ES'),
    ('X', 0o60,  0o77,  'U8 FX5-16EX/ES'),
    ('X', 0o100, 0o117, 'U9 FX5-16EX/ES'),
    ('X', 0o120, 0o137, 'U10 FX5-16EX/ES'),
    ('Y', 0o0,   0o37,  'U4 FX5U-64MT-ESS'),
    ('Y', 0o40,  0o57,  'U11 FX5-16EYT-ESS'),
    ('Y', 0o60,  0o77,  'U12 FX5-16EYT-ESS'),
    ('Y', 0o100, 0o117, 'U13 FX5-16EYT-ESS'),
    ('Y', 0o120, 0o137, 'U14 FX5-16EYT-ESS'),
    ('Y', 0o140, 0o157, 'U15 FX5-16EYT-ESS'),
]


def card(point):
    """'U11 FX5-16EYT-ESS, terminal Y2' - the card and ITS terminal name, as the drawing labels it."""
    kind, n = point[0], int(point[1:], 8)
    for k, lo, hi, name in CARDS:
        if k == kind and lo <= n <= hi:
            if lo == 0:
                return '%s, terminal %s' % (name, point)
            local = n - lo
            return '%s, terminal %s' % (name, ('X%02o' if kind == 'X' else 'Y%o') % local)
    return ''


COLUMNS = ['Point', 'Read', 'Commands', 'Tag', 'Wire', 'Sheet', 'Relay', 'RelaySheet', 'Coil', 'CoilWire',
           'CoilSheet', 'Valve', 'Cylinder', 'Hoses', 'Hose', 'PnSheet', 'Text', 'Plc', 'Card', 'Via', 'Measure', 'Pressure']
COLUMNS += ['%s.%s' % (c, lang) for lang in sorted(TRANSLATED) for c in TRANSLATED_COLUMNS]


def octal_key(point):
    return (point[0], int(point[1:], 8))


def mirrors():
    """Point -> the relay the HMI reads it at, from IoList.csv."""
    read = {}
    path = os.path.join(MACHINE, 'IoList.csv')
    for line in io.open(path, encoding='utf-8-sig'):
        line = line.strip()
        if not line or line.startswith('#') or line.startswith('Label;'):
            continue
        f = line.split(';')
        if len(f) >= 4 and f[3].strip():
            read[f[2].strip().upper()] = f[3].strip().upper()
    return read


def commands():
    """Output -> the manual command bits named for it (MAN_x on M.. drives OUT_x on Y..)."""
    import xlrd
    sheet = xlrd.open_workbook(os.path.join(HERE, 'FA2010_DeviceList.xls')).sheet_by_name('PLC_Labels')
    man, out = {}, {}
    for r in range(sheet.nrows):
        m, label = str(sheet.cell_value(r, 0)).strip().upper(), str(sheet.cell_value(r, 1)).strip()
        if re.match(r'^M\d+$', m) and label.upper().startswith('MAN_'):
            man.setdefault(label[4:].lower(), []).append(m)
        if sheet.ncols > 10:
            y, label = str(sheet.cell_value(r, 9)).strip().upper(), str(sheet.cell_value(r, 10)).strip()
            if re.match(r'^Y[0-7]+$', y) and label.upper().startswith('OUT_'):
                out[y] = label[4:].lower()
    return {y: man.get(name, []) for y, name in out.items()}


def build():
    read = mirrors()
    cmds = commands()
    rows = {}
    for point, tag, wire, sheet, text in ELECTRICAL_INPUTS:
        rows[point] = dict(Point=point, Tag=tag, Wire=wire, Sheet=sheet, Text=text)
    for point, relay, wire, sheet, coil, coil_wire, coil_sheet, text in ELECTRICAL_OUTPUTS:
        rows[point] = dict(Point=point, Relay=relay, Wire=wire, Sheet=sheet, Coil=coil, CoilWire=coil_wire,
                           CoilSheet=coil_sheet, RelaySheet=sheet if relay else '', Text=text)
    for points, valve, cylinder, hoses, pn_sheet in PNEUMATIC:
        own = [re.sub(r'\s*\(.*?\)\s*$', '', h).strip() for h in hoses.split('/')]
        for i, p in enumerate(points):
            row = rows.setdefault(p, dict(Point=p, Text=cylinder))
            row.update(Valve=valve, Cylinder=cylinder, Hoses=hoses, PnSheet=pn_sheet,
                       Hose=own[i] if i < len(own) else '')
    for points, text in MEASURE:
        for p in points:
            rows.setdefault(p, dict(Point=p))['Measure'] = text
    for points, text in PRESSURE:
        for p in points:
            rows.setdefault(p, dict(Point=p))['Pressure'] = text
    for p, row in rows.items():
        row['Read'] = read.get(p, '')
        row['Commands'] = ' '.join(cmds.get(p, []))
        row['Plc'] = '1'
        row['Card'] = card(p)
    for relay, tag, wire, sheet, via, text in MODULE_INPUTS:
        rows[relay] = dict(Point=relay, Tag=tag, Wire=wire, Sheet=sheet, Via=via, Text=text, Plc='1')
    missing = set()
    for row in rows.values():
        for lang, texts in TRANSLATED.items():
            for c in TRANSLATED_COLUMNS:
                if row.get(c):
                    if row[c] in texts:
                        row['%s.%s' % (c, lang)] = texts[row[c]]
                    else:
                        missing.add('%s: %s' % (lang, row[c]))
    for m in sorted(missing):
        print('  no translation - ' + m)
    out = io.StringIO()
    out.write('# Kaban HMI wiring references - generated by tools/build_wiring.py from the machine\'s\n')
    out.write('# electrical drawing (134581 rev. 9: Sheet 12.6 = page 12, column 6) and pneumatic\n')
    out.write('# drawing (134628 rev. 9: PnSheet 2 E3 = page 2, zone E3). Point: the PLC address.\n')
    out.write('# Read: the relay the HMI reads it at. Commands: manual command bits that drive it.\n')
    out.write('# Card: PLC card and terminal. Via: read through a module. Measure / Pressure: the cylinder\'s.\n')
    out.write('# Measure.TR / Pressure.TR / Via.TR: the same in Turkish (the step-by-step help in Turkish).\n')
    out.write('# "#!" lines are read: the supply wires a voltage is measured against (the step-by-step help).\n')
    out.write('#! ZeroVoltWire=21\n')
    out.write('#! SensorSupplyWire=20\n')
    out.write(';'.join(COLUMNS) + '\n')
    for p in sorted(rows, key=lambda q: (1, q) if q.startswith('M') else (0,) + octal_key(q)):
        r = rows[p]
        out.write(';'.join(r.get(c, '').replace(';', ',') for c in COLUMNS) + '\n')
    path = os.path.join(MACHINE, 'Wiring.csv')
    with io.open(path, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(out.getvalue())
    return path, len(rows)


if __name__ == '__main__':
    path, n = build()
    print('%s: %d points' % (path, n))
