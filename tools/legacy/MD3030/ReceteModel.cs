using System;

namespace _MD3030.Presentation.KabanSystem
{
    public class ReceteModel
    {
        public ReceteModel() { }

        // public async Task SendReceteInfo(string subtype, string width, string height, string earWeidth, string ProcessInfo)
        public string Id { get; set; }
        public string Name { get; set; }//required
        public string Dxfid { get; set; }
        public string Brand { get; set; }
        public string Type { get; set; }
        public string Serie { get; set; }
        public string Subtype { get; set; }
        public string Width { get; set; }
        public string Height { get; set; }
        public string EarWeidth { get; set; }
        public string ProcessInfo { get; set; }



        public void fill(string id, string name, string dxfid, string brand, string type, string serie, string subtype, string width, string height, string idearWeidth, string processinfo)
        {
            Id = id;
            Name = name;
            Dxfid = dxfid;
            Brand = brand;
            Type = type;
            Serie = serie;
            Subtype = subtype;
            Width = width;
            Height = height;
            EarWeidth = idearWeidth;
            ProcessInfo = processinfo;
        }
    }
}
