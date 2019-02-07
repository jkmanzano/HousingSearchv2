/** Copyright 02 de Febrero de 2019 por Juan Carlos Manzano**/

//fn_contain_feature: checks if a string field contains a word
fn_contain_feature = function(ps, pss) { 
			                 vr=0;
			                 if (!ps) {
						       vr=0;
                             } else {
						       vr= (ps.indexOf(pss) != -1) ? 1 : 0;
						     };
							 
                             return (vr);
};							 
							 
								 
//fn_treatnewdocs: treat new documents
fn_treatnewdocs = function(pcolletion) {
		db[pcolletion].find().forEach(function(doco){
			var vfound=db.oferta_inmo.findOne({UID:doco.UID});
			if (vfound) {
				  if( doco.price != vfound.price){
					db.oferta_inmo.deleteMany( { UID: vfound.UID });
					db.oferta_inmo.insert( doco );	
			        db.oferta_inmo_his.insert( doco );
				  };
		    } else {
			   db.oferta_inmo.insert( doco );
			   db.oferta_inmo_his.insert( doco );
		    };
		});
};

//fn_getCodPostal: Get the Posta Code from the geo location
fn_getCodPostal = function(plocation) {
   var cursor_near = db.codspostals.aggregate([
       { "$geoNear": {
        "near": plocation, 
		"key": "location",
        "spherical": true,
        "distanceField": "distance",
        "maxDistance": 5000
    }},
    { "$sort": { "distance": 1 } },
    { "$limit": 1 }
   ]);
   
   var xlocation = cursor_near.hasNext() ? cursor_near.next() : null;
   var vresult= {cod_postal:NaN, des_area:NaN, des_city:NaN, des_state:NaN, des_country:NaN};
   var xcodpostal= NaN;
   var xdesproblacion= NaN;
   if (xlocation) {
       vresult["cod_postal"] = xlocation.CodPostal;
       vresult["des_area"] = xlocation.Descripcion;
       vresult["des_city"] = xlocation.Poblacion;
       vresult["des_state"] = "Comunidad de Madrid";
       vresult["des_country"] = "Spain";
   }
   

  return vresult;
};

//fn_normattributes: make sure that the attributes have the right data types
fn_normattributes = function(pcolletion) {
  var bulk = db[pcolletion].initializeUnorderedBulkOp();
  var counter = 0;
  db[pcolletion].find({}).forEach(function(data) {

    var updattr = {"$set": {}};
    updattr["$set"]["bathroom_number"] = parseInt(data.bathroom_number);
    updattr["$set"]["bedroom_number"] = parseInt(data.bedroom_number);
    updattr["$set"]["car_spaces"] = parseInt(data.car_spaces);
    updattr["$set"]["construction_year"] = parseInt(data.construction_year);
    updattr["$set"]["floor"] = parseInt(data.floor);
    updattr["$set"]["room_number"] = parseInt(data.room_number);
    updattr["$set"]["size"] = parseInt(data.size);
    updattr["$set"]["updated_in_days"] = parseInt(data.updated_in_days);
    updattr["$set"]["price"] = parseFloat(data.price);
    updattr["$set"]["price_rent_real"] = parseFloat(data.price_rent_real);
    updattr["$set"]["price_sale_real"] = parseFloat(data.price_sale_real);
    updattr["$set"]["price_rent_estimated"] = parseFloat(data.price_rent_estimated);
    updattr["$set"]["price_sale_estimated"] = parseFloat(data.price_sale_estimated);
    updattr["$set"]["price_rent_estimated_gp"] = parseFloat(data.price_rent_estimated_gp);
    updattr["$set"]["price_sale_estimated_gp"] = parseFloat(data.price_sale_estimated_gp);
    updattr["$set"]["price_rent_estimated_2"] = parseFloat(data.price_rent_estimated_2);
    updattr["$set"]["price_sale_estimated_2"] = parseFloat(data.price_sale_estimated_2);
    updattr["$set"]["price_rent_estimated_2_gp"] = parseFloat(data.price_rent_estimated_2_gp);
    updattr["$set"]["price_sale_estimated_2_gp"] = parseFloat(data.price_sale_estimated_2_gp);	
	updattr["$set"]["price_opcion_1"] = parseFloat(data.price_opcion_1);
    updattr["$set"]["price_opcion_2"] = parseFloat(data.price_opcion_2);
    updattr["$set"]["PER"] = parseFloat(data.PER);	
    updattr["$set"]["longitude"] = parseFloat(data.longitude);
    updattr["$set"]["latitude"] = parseFloat(data.latitude);
	updattr["$set"]["boxroom"] = 0;

				
	//print(updattr)
    bulk.find({"_id": data._id}).update(updattr);
    counter++;
    if (counter % 1000 == 0) {
        bulk.execute();
        bulk = db[pcolletion].initializeOrderedBulkOp();
    };	  
	
  });
  if (counter % 1000 != 0) bulk.execute();
};

//fn_richdata: rich fields data
fn_richdata = function(pcolletion) {
  var bulk = db[pcolletion].initializeUnorderedBulkOp();
  var counter = 0;
  db[pcolletion].find({}).forEach(function(data) {
	var x_location = {"type": "Point", "coordinates": {}};
	x_location["coordinates"]=[ parseFloat(data.longitude), parseFloat(data.latitude)];
	var vdatcpostal=fn_getCodPostal(x_location);
	
    var updattr = {"$set": {}};
	updattr["$set"]["location"] = x_location;
    updattr["$set"]["cod_postal"] = vdatcpostal["cod_postal"];
    updattr["$set"]["des_area"] = vdatcpostal["des_area"];
    updattr["$set"]["des_city"] = vdatcpostal["des_city"];
    updattr["$set"]["des_state"] = vdatcpostal["des_state"];
    updattr["$set"]["des_country"] = vdatcpostal["des_country"];	
	updattr["$set"]["boxroom"] = fn_contain_feature(data.keywords,"trastero");
    updattr["$set"]["car_spaces"] = fn_contain_feature(data.keywords,"garaje");	
   
	//print(updattr)
    bulk.find({"_id": data._id}).update(updattr);
    counter++;
    if (counter % 1000 == 0) {
        bulk.execute();
        bulk = db[pcolletion].initializeOrderedBulkOp();
    };	  
	
  });
  if (counter % 1000 != 0) bulk.execute();
};

//fn_deleteInValidDocs delete invalid documents
fn_deleteInValidDocs = function(pcolletion) {

   //delete documents with incorrect values in essential data values
   db[pcolletion].deleteMany( 
   { $or: [ { bathroom_number: { $lte: 0 } } ,
            { bedroom_number: { $lte: 0 } },
            { price: { $lte: 0 } },
            { size: { $lte: 0 } } ] });
   
   db[pcolletion].deleteMany( 
   { $or: [ { bathroom_number: NaN } , { bedroom_number: NaN }, { price: NaN },
            { size: NaN }, { latitude: NaN }, { longitude: NaN } ] });
   
   //delete documents that extreme values
   db[pcolletion].deleteMany( {$and : [ { listing_type: "buy" } ,{ size: {"$gt": 500} }] });
   db[pcolletion].deleteMany( {$and : [ { listing_type: "buy" } ,{price: {"$gt": 3000000} }] } );
   
   db[pcolletion].deleteMany({ $and: [ { listing_type: "rent" } ,  { size: {"$gt": 300} } ]});
   db[pcolletion].deleteMany({ $and: [ { listing_type: "rent" } ,  { price: {"$gt": 6000} } ]});
   
   //eliminate documents with postal codes that do not belong to the Comunidad de Madrid
   //db[pcolletion].deleteMany({ $and: [ { cod_postal: {"$ne": ""} } ,  { cod_postal: {"$lt": "28000"} } ]});
   //db[pcolletion].deleteMany({ $and: [ { cod_postal: {"$ne": ""} } ,  { cod_postal: {"$gt": "28999"} } ]});
   db[pcolletion].deleteMany({ cod_postal: NaN });
}


//fn_updateInValidValuesInDocs => Normalize attribute values
fn_updateInValidValuesInDocs = function(pcolletion) {

	db[pcolletion].updateMany({ $or: [ { car_spaces: { $lt: 0 } } , { car_spaces: NaN}]}, { $set: { car_spaces: 0 } });
    db[pcolletion].updateMany({ $or: [ { boxroom: { $lt: 0 } } , { boxroom: NaN}]}, { $set: { boxroom: 0 } });
	db[pcolletion].updateMany({ $or: [ { construction_year: { $lt: 0 } } , { construction_year: NaN}]}, { $set: { construction_year: 0 } });
	db[pcolletion].updateMany({ $or: [ { floor: { $lt: 0 } } , { floor: NaN}]}, { $set: { floor: 0 } });
	db[pcolletion].updateMany({ $or: [ { room_number: { $lt: 0 } } , { room_number: NaN}]}, { $set: { room_number: 0 } });
	db[pcolletion].updateMany({ $or: [ { price_rent_real: { $lt: 0 } } , { price_rent_real: NaN}]}, { $set: { price_rent_real: 0 } });
	db[pcolletion].updateMany({ $or: [ { price_sale_real: { $lt: 0 } } , { price_sale_real: NaN}]}, { $set: { price_sale_real: 0 } });
	db[pcolletion].updateMany({ $or: [ { price_rent_estimated: { $lt: 0 } } , { price_rent_estimated: NaN}]}, { $set: { price_rent_estimated: 0 } });
	db[pcolletion].updateMany({ $or: [ { price_sale_estimated: { $lt: 0 } } , { price_sale_estimated: NaN}]}, { $set: { price_sale_estimated: 0 } });
	db[pcolletion].updateMany({ $or: [ { price_rent_estimated_gp: { $lt: 0 } } , { price_rent_estimated_gp: NaN}]}, { $set: { price_rent_estimated_gp: 0 } });
	db[pcolletion].updateMany({ $or: [ { price_sale_estimated_gp: { $lt: 0 } } , { price_sale_estimated_gp: NaN}]}, { $set: { price_sale_estimated_gp: 0 } });
	db[pcolletion].updateMany({ $or: [ { price_rent_estimated_2: { $lt: 0 } } , { price_rent_estimated_2: NaN}]}, { $set: { price_rent_estimated_2: 0 } });
	db[pcolletion].updateMany({ $or: [ { price_sale_estimated_2: { $lt: 0 } } , { price_sale_estimated_2: NaN}]}, { $set: { price_sale_estimated_2: 0 } });
	db[pcolletion].updateMany({ $or: [ { price_rent_estimated_2_gp: { $lt: 0 } } , { price_rent_estimated_2_gp: NaN}]}, { $set: { price_rent_estimated_2_gp: 0 } });
	db[pcolletion].updateMany({ $or: [ { price_sale_estimated_2_gp: { $lt: 0 } } , { price_sale_estimated_2_gp: NaN}]}, { $set: { price_sale_estimated_2_gp: 0 } });	
	db[pcolletion].updateMany({ $or: [ { price_opcion_1: { $lt: 0 } } , { price_opcion_1: NaN}]}, { $set: { price_opcion_1: 0 } });
	db[pcolletion].updateMany({ $or: [ { price_opcion_2: { $lt: 0 } } , { price_opcion_2: NaN}]}, { $set: { price_opcion_2: 0 } });	
	db[pcolletion].updateMany({ $or: [ { PER: { $lt: 0 } } , { PER: NaN}]}, { $set: { PER: 0 } });
		
	//db[pcolletion].updateMany({},{$set : {"des_poblacion":NaN}},false,true);
}

// fn_main => main function
fn_main = function() {  
	print(new Date());
	// refresh data with new flats
	var _listCollections = db.getCollectionNames();
	_listCollections.sort();
	_listCollections.forEach(function(collname) {
		if (collname.indexOf("oferta_inmo_temp_") !=-1){
			print("collection:" + collname + ":" + db[collname].find().count());
			print("collection:oferta_inmo:" + db.oferta_inmo.find().count());
			print("collection:oferta_inmo_his:" + db.oferta_inmo_his.find().count());
			
			fn_normattributes(collname);
			fn_richdata(collname);
			fn_deleteInValidDocs(collname);
			fn_updateInValidValuesInDocs(collname);
			fn_treatnewdocs(collname);
			
            db[collname].drop();	
		};	
	});
	print(new Date());
}

// execution of fn_main 
fn_main();

print("Fin.");