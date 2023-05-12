using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalaLite.Models
{
    public class PmtDataModel 
    {
        //TODO: Define PmtDataModel... ints? doubles? values interested in
        public int EventId { get; set; }
        public uint Time { get; set; }
        public int Sort { get; set; }
        public string Row { get; set; }
        public string Column { get; set; }
        public float Laser1ForwardScatterWidth { get; set; }
        public float Laser1ForwardScatterHeight { get; set; }
        public float Laser2ForwardScatterWidth { get; set; }
        public float Laser2ForwardScatterHeight { get; set; }
        public float SideScatterHeight { get; set; }
        public float Laser1PMT1 { get; set; }
        public float Laser1PMT2 { get; set; }
        public float Laser1PMT3 { get; set; }
        public float Laser1PMT4 { get; set; }
        public float Laser1PMT5 { get; set; }
        public float Laser1PMT6 { get; set; }
        public float Laser2PMT1 { get; set; }
        public float Laser2PMT2 { get; set; }
        public float Laser2PMT3 { get; set; }
        public float Laser2PMT4 { get; set; }
        public float Laser2PMT5 { get; set; }
        public float Laser2PMT6 { get; set; }
    }
}

