-- Sample rows for the Profiles screen. Run after Schema.sql:
--     psql -d kaban -f SampleData.sql
INSERT INTO profiles (profile_code, company_name, serial, profile_type, sub_type,
                      width_mm, height_mm, melting_s, welding_s, temperature_c, clamping_mm, dxf_file)
VALUES
 ('0001000007','DECEUNINCK','Elegant','KASA','CONTALI KAPLAMALI',72.0,76.5,20.0,30.0,240,12.0,'Profiles\82_Disa_Kapi.dxf'),
 ('0001000104','DECEUNINCK','Elegant','KANAT','CONTALI KAPLAMALI',70.0,78.0,20.0,30.0,240,12.0,'Profiles\82_Disa_Kapi.dxf'),
 ('0001020307','DECEUNINCK','TecnoCorr2','Hoja Puerta','CONTALI KAPLAMALI',82.0,115.0,22.0,32.0,245,14.0,'Profiles\82_Disa_Kapi.dxf')
ON CONFLICT (profile_code) DO NOTHING;
