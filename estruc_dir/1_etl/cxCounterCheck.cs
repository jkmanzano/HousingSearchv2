using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace etl_inm_counters
{
    class cxCounterCheck
    {
        public long totalPages_T;   //Theoric
        public long totalRecords_T; //Theoric
        public long pages_treated_R; //Treated pages
        public long records_treated_R; //Treated records
        public long stored_R;          //records stored
        public long noBuyRent;       //records with type not buy o rent
        public long exitRecordsinBBDD;       //records in BBDD
    }
}
