using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SB3015.Jsons
{
    public static class JsonCreator
    {
        public static void SaveParametersAsJson(SBMachineParameters parameters)
        {
            // Try parsing inputs
            

            // Define JSON options
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            // Serialize to JSON string
            string json = JsonSerializer.Serialize(parameters, options);

            // Save to file
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "machine_parameters.json");
            File.WriteAllText(path, json);
        }
    }
}
