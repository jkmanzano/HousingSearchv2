-- Generate the drdl file to be used with ConectorBI
"C:\Program Files\MongoDB\Connector for BI\2.8\bin\mongodrdl.exe" --db inmobil --out C:\ESTRUC_DIR\4_ANALYSIS\schemainmobil.drdl

--Launch ConectorBI to connect Tableau with MongoDB DDBB
"C:\Program Files\MongoDB\Connector for BI\2.8\bin\mongosqld.exe" --schema C:\ESTRUC_DIR\4_ANALYSIS\schemainmobil.drdl

dir "C:\ESTRUC_DIR\4_ANALYSIS\schemainmobil.drdl"



"C:\Program Files\MongoDB\Connector for BI\2.8\bin\mongosqld.exe" --addr=127.0.0.1:3307 --auth --mongo-username "biUser" --mongo-password "1234" --schema C:\ESTRUC_DIR\4_ANALYSIS\schemainmobil.drdl