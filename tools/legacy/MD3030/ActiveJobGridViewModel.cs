using KabanMachinesSystem.Data.Entities;
using System;

namespace _MD3030.Entities
{
    public class ActiveJobGridViewModel
    {
        // Job nesnesinden gelenler
        public DateTime ImportDate { get; set; }
        public string FileAliasName { get; set; }

        // CompanyProfile nesnesinden gelenler
        public string ProfileBrandType { get; set; } // Profil Markası (MainTableName)
        public string ProfileSerie { get; set; } // Profil Serisi (ProfileVCutting_Y)
        public int? Code { get; set; }

        // YENİ EKLENENLER: Tür ve Alt Tür
        public string ProfileType { get; set; } // Profil Türü
        public string ProfileSubType { get; set; } // Profil Alt Türü
        public Job job { get; set; }
    }
}
