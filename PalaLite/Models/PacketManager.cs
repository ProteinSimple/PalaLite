using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalaLite.Models
{
    internal class PacketManager
    {
        private CellPMTDataDecoder _decoder;

        //Cell Sorting Data
        int m_iBulkSortingCount;

        public PacketManager() 
        {
            _decoder = new CellPMTDataDecoder();
            _decoder.DataAvailableEventHandler += Decoder_DataAvailable;
            _decoder.DoneEventHandler += Decoder_Done;
        }

        static void Decoder_DataAvailable(object sender, DataAvailableEventArgs e)
        {
            // Do Something...
        }

        static void Decoder_Done(object sender, EventArgs e)
        {
            // Do Something...
        }
    }
}
