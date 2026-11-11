using System;
using System.Text.Json.Serialization;

namespace _MD3030.Presentation.Models
{
    public class ReceiptDepths
    {
        [JsonPropertyName("isMouldCuttingSelected")]
        public string IsMouldCuttingSelected { get; set; }

        [JsonPropertyName("isHelperCuttingSelected")]
        public string IsHelperCuttingSelected { get; set; }

        [JsonPropertyName("MainCuttingOffset")]
        public string MainCuttingOffset { get; set; }

        [JsonPropertyName("MouldCuttingDepth")]
        public string MouldCuttingDepth { get; set; }

        [JsonPropertyName("AuxCuttingDepth")]
        public string AuxCuttingDepth { get; set; }

        [JsonPropertyName("FinalCuttingDepth")]
        public string FinalCuttingDepth { get; set; }

        [JsonPropertyName("ToolRadius")]
        public object ToolRadius { get; set; } // Null geldiği için object veya string? kullanılabilir

        [JsonPropertyName("AluminiumDefaultSpeed")]
        public object AluminiumDefaultSpeed { get; set; }

        [JsonPropertyName("MouldCuttingDistance")]
        public string MouldCuttingDistance { get; set; }
    }
}
