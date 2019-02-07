
REM --Launch ETL process to get new data
CD "C:\ESTRUC_DIR\1_ETL"
"C:\ESTRUC_DIR\1_ETL\etl_inm.EXE"

REM --Launch js script to clean data
CD "C:\ESTRUC_DIR\2_CLEAN_NOR"
"C:\Program Files\MongoDB\Server\4.0\bin\mongo.exe" localhost:27017/inmobil C:\ESTRUC_DIR\2_CLEAN_NOR\scr_norm.js

REM --Launch R script to apply model
CD "C:\ESTRUC_DIR\3_MODEL"
"C:\Program Files\R\R-3.5.1\bin\Rscript.exe" --verbose "C:\ESTRUC_DIR\3_MODEL\scr_model.r"
