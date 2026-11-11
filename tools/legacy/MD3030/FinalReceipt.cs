using KabanMachinesSystem.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _MD3030.Presentation.Models
{
    public class FinalReceipt
    {
        public string Id { get; set; }
        public string SelectedReceipt { get; set; }
        public string ProcessCode { get; set; }
        public string ProcessDetail { get; set; }
        public string XPosition { get; set; }
        public string ZPosition { get; set; }
        public string RY1 { get; set; }
        public string Y2 { get; set; }
        public string Speed { get; set; }
        public ReceiptType ReceiptType { get; set; } = ReceiptType.MainReceipt;
    }

    public class ReceiptRoot
    {
        public List<FinalReceipt> Receipts { get; set; }
    }
}
