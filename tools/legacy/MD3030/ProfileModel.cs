
using System;

namespace _MD3030.Presentation.KabanSystem
{
    public class ReceiptModel
    {
        public Guid ID { get; set; } = Guid.NewGuid();
        public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public Guid EditBy { get; set; }
        public DateTime EditDate { get; set; } = DateTime.UtcNow;
        public string Name { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid ProfileID { get; set; }

    }

}
