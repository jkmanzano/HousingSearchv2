# Copyright 07 de Febrero de 2019 por Juan Carlos Manzano

# load R libraries 
#install.packages("mongolite")
#install.packages("jsonlite")
#install.packages("stringr")
#install.packages("MASS")
#install.packages("caret")

#load R libraries 
library(mongolite)
library(jsonlite)
library(stringr)
library(MASS)
library(caret)

#Conexion with data base 
vurlmongo<-"mongodb://localhost:27017/inmobil"

dt_oferta_inmo <-  mongo(collection = "oferta_inmo", db="inmobil", url = vurlmongo, verbose = TRUE)
dt_codspostals <-  mongo(collection = "codspostals", db="inmobil", url = vurlmongo, verbose = TRUE)

# get the last dates
df_cp_lstupd <- dt_codspostals$aggregate('[{"$group":{"_id": null, "maxQty": { "$max" : "$flastupd" }}}]')
vlastdate<-paste(substr(df_cp_lstupd[1, ]['maxQty'],1,10), "0000", sep="")

# Definition of functions used

        # fcheck_stepAIC: check the stepAIC
		# This function permit check the AIC for a model previusly to apply StepAIC, and avoid the error 
		# error message "AIC is not defined for this model, so 'stepAIC' cannot proceed"
		# It use the formula AICc explained in https://en.wikipedia.org/wiki/Akaike_information_criterion#AICc
		# This function has been inspired on R-Forge Forum: http://r-forge.wu-wien.ac.at/forum/forum.php?thread_id=31297&forum_id=995&group_id=302
        fcheck_stepAIC <- function (object, scope, scale = 0, AICc=T, direction = c("both", "backward", "forward"), trace = 1, keep = NULL, steps = 1000, use.start = FALSE, k = 2, ...) 
        {
             xreturn<-1
             
             fit <- object
             bAIC <- extractAIC(fit, scale, k = k, ...)
             bAIC <- bAIC[2L]
             ## MODIFICATION: transform AIC in AICc
             if(AICc){bAIC <- bAIC + (2*length(object$coef)*(length(object$coef)+1))/(length(object$res)-length(object$coef)-1)}
			 
			 #stop("AIC is not defined for this model, so 'stepAIC' cannot proceed")
             if (is.na(bAIC)) xreturn<-0            
             if (bAIC == -Inf) xreturn<-0
		     if (bAIC <0) xreturn<-0

             return(xreturn)
        }

        # fn_lm_price_rent: Get estimation rent price
		fn_lm_price_rent<- function(pdata, con) {
	          xlongitude<-pdata[1, "longitude"]
			  xlatitude<-pdata[1, "latitude"]
			  xprice_rent<-0
			  xrsquared<-0
		  
		      							  		  
              xloop1query<- paste0('[ { "$geoNear": {"near": { "type" : "Point", "coordinates" : [', xlongitude, ', ', xlatitude,' ] }, 
		                                    "key": "location", "spherical": true, "distanceField": "distance", "maxDistance": 10000,
		      							  "query": {"listing_type":"rent", "bathroom_number": { "$lt": 4 }}
                                           }
		      	           },
                             { "$sort": { "distance": 1 } },
                             { "$limit": 8 }
                           ]');		
						   
              df_alquiler_loc <- con$aggregate(pipeline = xloop1query)  	
              
	          # check to rent dataset is empty
              if(nrow(df_alquiler_loc) > 0){
               df_alquiler_train <- subset(df_alquiler_loc, , select=c(bathroom_number,bedroom_number,car_spaces,price,size,boxroom))
               df_alquiler_train[is.na(df_alquiler_train)] <- 0
               lm.precio_alquiler <- lm(price~., data=df_alquiler_train)
			   vs<-summary(lm.precio_alquiler)
			   xrsquared<-vs$r.squared
			   
			   dostepAIC<-fcheck_stepAIC(lm.precio_alquiler)
					 	 traza<-sprintf("r.dostepAIC:[%.3f]\n", dostepAIC)
						 print(traza)	
						 			   
			   if (dostepAIC==1) {
			     lm.precio_alquiler_bis<- stepAIC(lm.precio_alquiler)
			     vs<-summary(lm.precio_alquiler_bis)
				 xrsquared<-vs$r.squared					 
			   } else
			     lm.precio_alquiler_bis<- lm.precio_alquiler
			   
               lm.predict_alquiler <- predict(lm.precio_alquiler_bis, pdata)	
			   xprice_rent<-as.numeric(lm.predict_alquiler[1])
			   

						 #vcm<-confusionMatrix(lm.predict_alquiler, pdata$performance)$overall['Accuracy']
					 	 traza<-sprintf("r.squared:[%.3f], Accuracy:[%.3f]\n", vs$r.squared, 0)
						 print(traza)	
						 
	          }
              		
		      if (xprice_rent<0){ xprice_rent=0}
			  
			  rlist<-list("price_rent" = xprice_rent, "rsquared" = xrsquared)
			  
			  return(rlist)
		}

		# fn_lm_price_buy: Get estimation sale price
		fn_lm_price_buy<- function(pdata, con) {
			  xlongitude<-pdata[1, "longitude"]
			  xlatitude<-pdata[1, "latitude"]
			  xprice_buy<- 0
			  
              xloop1query<- paste0('[ { "$geoNear": {"near": { "type" : "Point", "coordinates" : [', xlongitude, ', ', xlatitude,' ] }, 
		                                    "key": "location", "spherical": true, "distanceField": "distance", "maxDistance": 10000,
		      							  "query": {"listing_type":"buy", "bathroom_number": { "$lt": 4 }}
                                           }
		      	           },
                             { "$sort": { "distance": 1 } },
                             { "$limit": 8 }
                           ]');		
              df_venta_loc <- con$aggregate(pipeline = xloop1query)  	
              
	          # check to rent dataset is empty
              if(nrow(df_venta_loc) > 0){
               df_venta_train <- subset(df_venta_loc, , select=c(bathroom_number,bedroom_number,car_spaces,price,size,boxroom))
               df_venta_train[is.na(df_venta_train)] <- 0
               lm.precio_venta <- lm(price~., data=df_venta_train)
			   vs<-summary(lm.precio_venta)
			   xrsquared<-vs$r.squared
			   
			   dostepAIC<-fcheck_stepAIC(lm.precio_venta)					 			   			 
			   if (dostepAIC==1) {
			     lm.precio_venta_bis<- stepAIC(lm.precio_venta)
			     vs<-summary(lm.precio_venta_bis)
				 xrsquared<-vs$r.squared				 
			   } else
			     lm.precio_venta_bis<- lm.precio_venta
				 
               lm.predict_venta <- predict(lm.precio_venta_bis, pdata)	
			   xprice_buy<-as.numeric(lm.predict_venta[1])	
	          }
              	
		      if (xprice_buy<0){ xprice_buy=0}
			  
			  rlist<-list("price_buy" = xprice_buy, "rsquared" = xrsquared)
			  
			  return(rlist)
		}
		
		# fn_PER: Do PER indicator calculation
		fn_PER<- function(pprice_buy, pprice_rent) {

              if (pprice_rent==0) {
			    xPER=0
		      } else {
		        xPER<-round(pprice_buy/(pprice_rent*12))
              }
			  
              if ( length(xPER)==0 ) {
		          xPER = 0
	          }
			  
			  return(xPER)
		}

		# fn_cal_DAT_buy: Prepare sale offer data
        fn_cal_DAT_buy<- function(pcon) {
		    est_PER<-0
			
		    querycp<-paste0('{"$and":[ {"listing_type":"buy"}, {"price_sale_real":0}]}')
            datcp<-pcon$distinct("cod_postal", query = querycp)		
			for (rowcp in 1:length(datcp)) {
			    xcp<- datcp[rowcp]
				if (!(xcp==NaN)) {
			      queryx<-paste0('{"$and":[ {"listing_type":"buy"}, {"cod_postal":"', xcp, '"}]}')
     		      vdata<-pcon$find(query = queryx, field = '{}')
				  if (nrow(vdata)>0){
       			    for (row in 1:nrow(vdata)) {
       	             xdf_venta_pend=vdata[row,]
       	  	         x_id<-vdata[row, "_id"]
  					 
                     real_price_buy<-as.numeric(vdata[row, "price"])
					 	 traza<-sprintf("memory:[%.3f], cod_postal:%s, id:%s, real_price_buy:[%.3f]\n", memory.size(), xcp, x_id, real_price_buy)
						 print(traza)
						 
                     dat_rent_estimated<-fn_lm_price_rent(xdf_venta_pend, pcon)
					 est_price_rent<-dat_rent_estimated$price_rent
					 est_price_rent_gp<-dat_rent_estimated$rsquared
					 
  					 if (est_price_rent>0){
                       est_PER<-fn_PER(real_price_buy, est_price_rent)
       			       
					   dat_price_estimated<-fn_lm_price_buy(xdf_venta_pend, pcon)					 
					   est_price_buy<-dat_price_estimated$price_buy
					   est_price_buy_gp<-dat_price_estimated$rsquared						   
       	  	           #update indicator PER in MongoDB
                       pcon$update(query = paste0('{"_id": { "$oid" : "', x_id, '" } }'),
                                   update = paste0('{ "$set" : {"PER" :', est_PER, 
																', "price_sale_real": ', round(real_price_buy, digits = 0), 								   
								                                ', "price_rent_estimated": ', round(est_price_rent, digits = 0), 
																', "price_rent_estimated_gp": ', round(est_price_rent_gp*10, digits = 0), 
									                            ', "price_sale_estimated": ', round(est_price_buy, digits = 0), 																   
																', "price_sale_estimated_gp": ', round(est_price_buy_gp*10, digits = 0), 																
																'} }'), 
                        		   upsert = FALSE)
  					 }
     		        }
				  }
				}
				gc()
		    }			
		}
		
		# fn_cal_DAT_rent: Prepare sale offer rent
        fn_cal_DAT_rent<- function(pcon) {
		    est_PER<-0
			
		    querycp<-paste0('{"$and":[ {"listing_type":"rent"}, {"price_rent_real":0}]}')
            datcp<-pcon$distinct("cod_postal", query = querycp)		
			for (rowcp in 1:length(datcp)) {
			    xcp<- datcp[rowcp]
				if (!(xcp==NaN)) {
			      queryx<-paste0('{"$and":[ {"listing_type":"rent"}, {"cod_postal":"', xcp, '"}]}')
     		      vdata<-pcon$find(query = queryx, field = '{}')
				  if (nrow(vdata)>0){				  
  			        for (row in 1:nrow(vdata)) {
  	                 xdf_rent_pend=vdata[row,]
  	  	             x_id<-vdata[row, "_id"]
                     real_price_rent<-as.numeric(vdata[row, "price"])
					 
					 dat_price_estimated<-fn_lm_price_buy(xdf_rent_pend, pcon)					 
					 est_price_buy<-dat_price_estimated$price_buy
					 est_price_buy_gp<-dat_price_estimated$rsquared				 
  					 if (est_price_buy>0){
                         est_PER<-fn_PER(est_price_buy,real_price_rent)
					 
                         dat_rent_estimated<-fn_lm_price_rent(xdf_rent_pend, pcon)
					     est_price_rent<-dat_rent_estimated$price_rent
					     est_price_rent_gp<-dat_rent_estimated$rsquared
					   
  	  	                 #update indicator PER in MongoDB
                         pcon$update(query = paste0('{"_id": { "$oid" : "', x_id, '" } }'),
                                     update = paste0('{ "$set" : {"PER" :', est_PER, 
																   ', "price_rent_real": ', round(real_price_rent, digits = 0), 
									                               ', "price_sale_estimated": ', round(est_price_buy, digits = 0), 																   
																   ', "price_sale_estimated_gp": ', round(est_price_buy_gp*10, digits = 0), 
								                                   ', "price_rent_estimated": ', round(est_price_rent, digits = 0), 
																   ', "price_rent_estimated_gp": ', round(est_price_rent_gp*10, digits = 0), 																   
																   '} }'), 
                        			 upsert = FALSE)
  					 }
       		        }
				  }
				}
				gc()
		    }			
		}

		# fn_main: main function
		fn_main<- function(pcon) {
		    fn_cal_DAT_buy(pcon)
			fn_cal_DAT_rent(pcon)
		}

# execution of mail function		
fn_main(dt_oferta_inmo)

	 
rm(dt_oferta_inmo)
rm(dt_codspostals)
gc()