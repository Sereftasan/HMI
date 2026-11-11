using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _MD3030.Entities
{
    public class JobEntity
    {
        public string OrderNo { get; set; }
        public string TrolleyNo { get; set; }
        public string TrolleyShelfNo { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string ProfileCode { get; set; }

        public string CustomBarcode { get; set; }
}

    
}
