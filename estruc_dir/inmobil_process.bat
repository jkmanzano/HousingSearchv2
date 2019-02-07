
SET xHOUR=%time:~0,2%
SET xtimestamp9=%date:~-4%%date:~3,2%%date:~0,2%_0%time:~1,1%%time:~3,2%%time:~6,2% 
SET xtimestamp24=%date:~-4%%date:~3,2%%date:~0,2%_%time:~0,2%%time:~3,2%%time:~6,2%

if "%xHOUR:~0,1%" == " " (SET logtimestamp=%xtimestamp9%) else (SET logtimestamp=%xtimestamp24%)

ECHO %xtimestamp%

set logfilename=LOG_%logtimestamp%.TXT

REM
CD "C:\ESTRUC_DIR"
"C:\ESTRUC_DIR\inmobil_subprocess.BAT" > C:\ESTRUC_DIR\LOGS\%logfilename%
