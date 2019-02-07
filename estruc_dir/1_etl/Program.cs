using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Web.Script.Serialization;
using System.Globalization;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDB.Driver.Core;
using etl_inm_connection;
using etl_inm_counters;

namespace etl_inm
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //main
        static void Main(string[] args)
        {
            cxDataConnection vDataConneciton;
            vDataConneciton = new cxDataConnection();
            iniDataConnections(vDataConneciton);

            cxCounterCheck vCounterCheck;
            vCounterCheck = new cxCounterCheck();

            MongoClient _MDBclient = new MongoClient(vDataConneciton.m_conexion);

            mongo_inicioBDMongo(_MDBclient, vDataConneciton);
            mongo_cargaPoblaciones(_MDBclient, vDataConneciton);
            
            //Proccess references or areas defined
            for (int i = 0; i < vDataConneciton._Areas.Length; i++)
            {
                vDataConneciton._area = vDataConneciton._Areas[i];
                Console.Write("*{0}", vDataConneciton._area);

                log.Info("");
                log.Info("Starting...");
                log.Info(string.Format(">>>####Area:{0}", vDataConneciton._area));
                log.Info("=SALE=");
                iniCounterCheck(vCounterCheck);
                procesarDatosXTipo(_MDBclient, vDataConneciton, "Buy", vCounterCheck);

                log.Info("--SALE:Summary--");
                log.InfoFormat("Theory - Pages: {0} Records:{1}", vCounterCheck.totalPages_T, vCounterCheck.totalRecords_T);
                log.Info(string.Format("Number of treated pages: {0}", vCounterCheck.pages_treated_R));
                log.Info(string.Format("Number of treated flats: {0}", vCounterCheck.records_treated_R));
                log.Info(string.Format("Number of stored flats: {0}", vCounterCheck.stored_R));
                log.Info(string.Format("Number of existing records: {0}", vCounterCheck.exitRecordsinBBDD));
                log.Info(string.Format("Number of flats not buy and not rent: {0}", vCounterCheck.noBuyRent));

                log.Info("=RENT=");
                iniCounterCheck(vCounterCheck);

                procesarDatosXTipo(_MDBclient, vDataConneciton, "Rent", vCounterCheck);

                log.Info("--RENT:Summary--");
                log.Info(string.Format("Pages: {0} Records:{1}", vCounterCheck.totalPages_T, vCounterCheck.totalRecords_T));
                log.Info(string.Format("Number of treated pages: {0}", vCounterCheck.pages_treated_R));
                log.Info(string.Format("Number of treated flats: {0}", vCounterCheck.records_treated_R));
                log.Info(string.Format("Number of stored flats: {0}", vCounterCheck.stored_R));
                log.Info(string.Format("Number of existing records: {0}", vCounterCheck.exitRecordsinBBDD));
                log.Info(string.Format("Number of flats not buy and not rent: {0}", vCounterCheck.noBuyRent));

            }

            mongo_finBDMongo(_MDBclient, vDataConneciton).Wait();
           

            Console.WriteLine("fin");
        }

        //Initialize data connection
        private static void iniDataConnections(cxDataConnection pDataConneciton)
        {
            string vs_refs = "";
            Random num_rnd = new Random();
            string vs_now = DateTime.Now.ToString("yyyyMMddHHmmss");

            var myReaderAppSettings = new System.Configuration.AppSettingsReader();
            pDataConneciton.baseUriNes_B = @myReaderAppSettings.GetValue("URL_API_B", typeof(string)).ToString();
            pDataConneciton.baseUriNes_R = @myReaderAppSettings.GetValue("URL_API_R", typeof(string)).ToString();
            pDataConneciton.m_conexion = myReaderAppSettings.GetValue("AC_conexion", typeof(string)).ToString();
            pDataConneciton.mdb_inmobil = myReaderAppSettings.GetValue("AC_dbinmobil", typeof(string)).ToString();
            pDataConneciton.mc_oferta_inmo = myReaderAppSettings.GetValue("AC_colloferta_inmo", typeof(string)).ToString() + "_temp_" + vs_now + "_"+ num_rnd.Next(10000, 100001).ToString();
            pDataConneciton.mc_cods_pob = myReaderAppSettings.GetValue("AC_coll_cods_pob", typeof(string)).ToString();

            vs_refs = myReaderAppSettings.GetValue("AC_RefS", typeof(string)).ToString();
            pDataConneciton._Areas = vs_refs.Split(';').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

            pDataConneciton.num_cp_tr = myReaderAppSettings.GetValue("AC_num_cp_tr", typeof(string)).ToString();

        }

        //Initialize check counters
        private static void iniCounterCheck(cxCounterCheck pCounterCheck)
        {
            pCounterCheck.totalPages_T = 0;
            pCounterCheck.totalRecords_T = 0;
            pCounterCheck.pages_treated_R = 0;
            pCounterCheck.records_treated_R = 0;
            pCounterCheck.stored_R = 0;
            pCounterCheck.noBuyRent = 0;
            pCounterCheck.exitRecordsinBBDD = 0;
        }

        private static async void mongo_inicioBDMongo(MongoClient pm_client, cxDataConnection pDataConneciton)
        {
            var _db = pm_client.GetDatabase(pDataConneciton.mdb_inmobil);

            var coleccionOfertai = _db.GetCollection<BsonDocument>(pDataConneciton.mc_oferta_inmo);

            var _filter = new BsonDocument();

            var result = await coleccionOfertai.DeleteManyAsync(_filter);
        }

        //load the postal code to be proccesed
        private static void mongo_cargaPoblaciones(MongoClient pm_client, cxDataConnection pDataConneciton)
        {
            StringCollection myList = new StringCollection();
            int vnum_cp = 0;

            try
            {
               vnum_cp = Int32.Parse(pDataConneciton.num_cp_tr);
               myList.AddRange(pDataConneciton._Areas);

               var _db = pm_client.GetDatabase(pDataConneciton.mdb_inmobil);
               
               var colcpob = _db.GetCollection<BsonDocument>(pDataConneciton.mc_cods_pob);

               //"{flastupd: 1}"
               if (vnum_cp > 0)
               {
                   var _filter1 = Builders<BsonDocument>.Filter.Ne("Descripcion", "");
                   var listBD = colcpob.Find(_filter1).Sort(Builders<BsonDocument>.Sort.Ascending("flastupd")).Limit(vnum_cp).ToList();

                   foreach (var docx in listBD)
                   {
                       string stemp= docx["Descripcion"].ToString();
                       if (!(myList.Contains(stemp)))
                       {
                           myList.Add(stemp);
                       }
                   }

                   
                   var _filter2 = Builders<BsonDocument>.Filter.Eq("Descripcion", "");
                   var listBD2 = colcpob.Find(_filter2).Sort(Builders<BsonDocument>.Sort.Ascending("flastupd")).Limit(vnum_cp).ToList();

                   foreach (var docx in listBD2)
                   {
                       string stemp = docx["Poblacion"].ToString();
                       if (!(myList.Contains(stemp)))
                       {
                           myList.Add(stemp);
                       }
                   }                   
               }
               pDataConneciton._Areas = myList.Cast<string>().ToArray<string>();

            }
            catch (Exception e)
            {
                log.Error("Error1:" + e.Message);
            }

        }

        //Update parameters in mongodb
        private static async Task mongo_finBDMongo(MongoClient pm_client, cxDataConnection pDataConneciton)
        {
            string tempSTR;
            string vs_now = DateTime.Now.ToString("yyyyMMddHHmmss");


            try
            {
                var _db = pm_client.GetDatabase(pDataConneciton.mdb_inmobil);
                //_db.DropCollection(pDataConneciton.mc_oferta_inmo);

                var colcpob = _db.GetCollection<BsonDocument>(pDataConneciton.mc_cods_pob);

                for (int i = 0; i < pDataConneciton._Areas.Length; i++)
                {
                    tempSTR = pDataConneciton._Areas[i];
                    var result1 = await colcpob.UpdateManyAsync(
                                                   Builders<BsonDocument>.Filter.Eq("Poblacion", tempSTR),
                                                   Builders<BsonDocument>.Update.Set("flastupd", vs_now)
                                                   );

                    if (result1.ModifiedCount == 0)
                    {
                        var result2 = await colcpob.UpdateManyAsync(
                                                       Builders<BsonDocument>.Filter.Eq("Descripcion", tempSTR),
                                                       Builders<BsonDocument>.Update.Set("flastupd", vs_now)
                                                       );
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("Error2:" + e.Message);
            }

        }

        //process data by type (buy or rent)
        private static void procesarDatosXTipo(MongoClient pm_client, cxDataConnection pDataConneciton, string ptipo, cxCounterCheck pCounterCheck)
        {
            string vs_url="";
            string vs_Pagina;
            long vn_Paginas = 0;
            long vn_Records = 0;

            if (ptipo == "Buy") {
                vs_url = pDataConneciton.baseUriNes_B;
            } else if (ptipo == "Rent") {
                vs_url = pDataConneciton.baseUriNes_R;
            }


            if (vs_url != "")
            {
                vs_url = vs_url + "&place_name={0}";
                vs_url = string.Format(vs_url, pDataConneciton._area);
                vs_Pagina = leerPagina(vs_url, 0);
                vn_Paginas = obtener_Num_Paginas(vs_Pagina);
                vn_Records = obtener_Num_Records(vs_Pagina);
                pCounterCheck.totalPages_T = vn_Paginas;
                pCounterCheck.totalRecords_T = vn_Records;

                for (int i = 1; i <= vn_Paginas + 1; i++)
                {
                    pCounterCheck.pages_treated_R++;
                    vs_Pagina = leerPagina(vs_url, i);
                    tratarPagina(pm_client, pDataConneciton, vs_Pagina, pCounterCheck);

                    Console.Write("/{0}", i.ToString());
                }
            }
        }

        //read page from web service
        private static string leerPagina(string psurl, long pPagina)
        {
            string vs_lurl;
            string s;
            using (WebClient client = new WebClient())
            {

                client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                if (pPagina > 0) {
                    psurl = psurl + "&page={0}";
                    vs_lurl = string.Format(psurl, pPagina);
                } else 
                    vs_lurl= psurl;

                Stream data = client.OpenRead(vs_lurl);
                StreamReader reader = new StreamReader(data);
                s = reader.ReadToEnd();
                data.Close();
                reader.Close();
            }
            return s;
        }

        //get the number of pages from the answer of web service
        private static long obtener_Num_Paginas(string pPagina)
        {
            long result = 0;
            var respuestaGlobalObject = JObject.Parse(pPagina);
            try
            {
                result = (long)Convert.ToDouble(respuestaGlobalObject["response"]["total_pages"].ToString());
            }
            catch (Exception e)
            {
                result=1;
            }

            return result;            
        }

        //get the number of recorsd from the answer of web service
        private static long obtener_Num_Records(string pPagina)
        {
            long result = 0;
            var respuestaGlobalObject = JObject.Parse(pPagina);

            try{
               result = (long)Convert.ToDouble(respuestaGlobalObject["response"]["total_results"].ToString());
            }
            catch (Exception e)
            {
               result=0;
            }
            return result;
        }

        //treat the page from web service
        private static void tratarPagina(MongoClient pm_client, cxDataConnection pDataConneciton, string pPagina, cxCounterCheck pCounterCheck)
        {
            string vs_piso="";
            string v_zipcode;
            string vx_uid;
            string vx_tipoOper;
            int vn_posx=0;

            //DateTime vd_today = DateTime.Today;
            string vs_now = DateTime.Now.ToString("yyyyMMddHHmmss");

            var respuestaGlobalObject = JObject.Parse(pPagina);

            JArray pisosObject = JArray.Parse(respuestaGlobalObject["response"]["listings"].ToString());
            foreach (JObject item in pisosObject)
            {
                pCounterCheck.records_treated_R++;

                var pisoObj = JObject.Parse(item.ToString());

                vx_uid = pisoObj["lister_url"].ToString();

                vn_posx = vx_uid.IndexOf("detail/");

                if (vn_posx == 0 || vn_posx == -1) {
                    vn_posx = vx_uid.IndexOf("detail-int/");
                    vn_posx = vn_posx + 11;
                } else
                    vn_posx = vn_posx + 7;

                vx_uid = vx_uid.Substring(vn_posx, 25);

                v_zipcode = "";


                // Store the property
                vx_tipoOper = pisoObj["listing_type"].ToString();

                pisoObj = ActualizarJSON(pisoObj, "cod_postal", v_zipcode);
                pisoObj = ActualizarJSON(pisoObj, "PER", "0");
                pisoObj = ActualizarJSON(pisoObj, "UID", vx_uid);
                pisoObj = ActualizarJSON(pisoObj, "price_rent_real", "0");
                pisoObj = ActualizarJSON(pisoObj, "price_sale_real", "0");
                pisoObj = ActualizarJSON(pisoObj, "price_rent_estimated", "0");
                pisoObj = ActualizarJSON(pisoObj, "price_sale_estimated", "0");
                pisoObj = ActualizarJSON(pisoObj, "price_rent_estimated_gp", "0");
                pisoObj = ActualizarJSON(pisoObj, "price_sale_estimated_gp", "0");
                pisoObj = ActualizarJSON(pisoObj, "price_rent_estimated_2", "0");
                pisoObj = ActualizarJSON(pisoObj, "price_sale_estimated_2", "0");
                pisoObj = ActualizarJSON(pisoObj, "price_rent_estimated_2_gp", "0");
                pisoObj = ActualizarJSON(pisoObj, "price_sale_estimated_2_gp", "0");
                pisoObj = ActualizarJSON(pisoObj, "price_opcion_1", "0");
                pisoObj = ActualizarJSON(pisoObj, "price_opcion_2", "0");
                pisoObj = ActualizarJSON(pisoObj, "des_area", "");
                pisoObj = ActualizarJSON(pisoObj, "des_city", "");
                pisoObj = ActualizarJSON(pisoObj, "des_state", "");
                pisoObj = ActualizarJSON(pisoObj, "des_country", "");
                pisoObj = ActualizarJSON(pisoObj, "boxroom", "0");
                pisoObj = ActualizarJSON(pisoObj, "FInsert", vs_now);
                pisoObj = ActualizarJSON(pisoObj, "FUpdate", vs_now);

                if (vx_tipoOper == "buy" || vx_tipoOper == "rent") {
                    vs_piso = pisoObj.ToString();
                    grabarInmueble(pm_client, pDataConneciton, pCounterCheck, vs_piso);
                } else{
                    pCounterCheck.noBuyRent++;
                }
            }

        }

        //update a json
        private static JObject ActualizarJSON(JObject porigen, string pnombre, string pvalor)
        {
            var lprop = porigen.Property(pnombre);

            if (lprop == null) {
                lprop = new JProperty(pnombre, pvalor);
                porigen.Add(lprop);
            }  else
                lprop.Value = pvalor;

            return porigen;
        }

        // save property in mongodb
        private static async void grabarInmueble(MongoClient pm_client, cxDataConnection pDataConneciton, cxCounterCheck pCounterCheck, string pInmueble)
        {
            DateTime vd_today = DateTime.Today;
            string vs_uid;
            double vd_price;

            var _db = pm_client.GetDatabase(pDataConneciton.mdb_inmobil);

            var coleccionOfertai = _db.GetCollection<BsonDocument>(pDataConneciton.mc_oferta_inmo);

            var respuestaGlobalObject = JObject.Parse(pInmueble);

            if (pInmueble != "")
            {
               var bsonDoc = BsonDocument.Parse(pInmueble.ToString());

               vs_uid = bsonDoc["UID"].ToString();
               vd_price = bsonDoc["price"].ToDouble()+1;
               var _filter = Builders<BsonDocument>.Filter.Eq("UID", vs_uid);
               var res_find = coleccionOfertai.Find(_filter).ToList();

               if (res_find.Count==0)
               {
                   await coleccionOfertai.InsertOneAsync(bsonDoc);
                   pCounterCheck.stored_R++;
               }
               else
                   pCounterCheck.exitRecordsinBBDD++;            
            }

            Console.Write(".");
        }
               
}
    
}
