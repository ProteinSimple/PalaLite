using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using CyUSB;
using PalaLite.Models;

namespace PalaLite
{
    internal class Program
    {

        static private CellPMTDataDecoder _decoder;
        static private MicroController _mcu;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(App_ProcessExit);
            System.Diagnostics.Process myProcess = System.Diagnostics.Process.GetCurrentProcess();

            myProcess.PriorityClass = System.Diagnostics.ProcessPriorityClass.High;

            _decoder = new CellPMTDataDecoder();
            _decoder.DataAvailableEventHandler += Decoder_DataAvailable;

            _mcu = new MicroController();
            _mcu.packetManager.DoneEventHandler += PacketLimitReached;
            _mcu.packetManager.PacketAvailableEventHandler += PacketAvailable;
            _mcu.StartPMT();
        }

        static void App_ProcessExit(object sender, EventArgs e)
        {
            // Stop Data Streaming on CTL+C
            _mcu.StopPMT();
        }
        static void Decoder_DataAvailable(object sender, DataAvailableEventArgs e)
        {
            // Do Something...
        }

        static void Decoder_Done(object sender, EventArgs e)
        {
            // Do Something...
        }

        static void PacketAvailable(object sender, EventArgs e)
        {
            bool success;
            byte[] data;
            while (_mcu.packetManager.DataAvailable())
            {
                (success, data) = _mcu.packetManager.GetNextPacket();
                if (success) { AnalyzePacket(data); }
            }
        }

        static void PacketLimitReached(object sender, EventArgs e)
        {
            // Stop Data Streaming
            _mcu.StopPMT();
        }

        static void AnalyzePacket(byte[] buf)
        {
            _decoder.AnalyzePacket
            (
                packet: buf,
                selectedChannels: 0, //m_iSelectedChannels,
                channelNumber: 0, //m_iChannelNumber,
                plateRow: 1, //m_iCurPlateRow,
                plateColumn: 1 //m_iCurPlateCol
            );
        }
    }
}
