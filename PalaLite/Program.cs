using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
            try
            {
                Console.CancelKeyPress += new ConsoleCancelEventHandler(App_ProcessExit);
                System.Diagnostics.Process myProcess = System.Diagnostics.Process.GetCurrentProcess();

                myProcess.PriorityClass = System.Diagnostics.ProcessPriorityClass.High;

                _decoder = new CellPMTDataDecoder();
                _decoder.DataAvailableEventHandler += Decoder_DataAvailable;

                _mcu = new MicroController();
                _mcu.packetManager.DoneEventHandler += PacketLimitReached;
                _mcu.packetManager.PacketAvailableEventHandler += PacketAvailable;
                _mcu.StartDataAcquisitionEventHandler += StartDataAcquisition;
                _mcu.StartPMT();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + e.StackTrace);
            }
        }

        static void StartDataAcquisition(object sender, EventArgs e)
        {
            DataAcqusitionThread();
        }

        static void DataAcqusitionThread()
        {
            byte[] buffer = new byte[512];

            //_acquireData = true; //Start usb data transfer

            // Setup iso-transfer buffers size 512 and one packet per transfer
            byte[] cmdBufs = new byte[512];
            byte[] xferBufs = new byte[512];
            byte[] ovLaps = new byte[512];
            ISO_PKT_INFO[] pktsInfo = new ISO_PKT_INFO[1];

            //Pin the data buffer memory, so GC won't touch the memory
            GCHandle cmdBufferHandle = GCHandle.Alloc(cmdBufs[0], GCHandleType.Pinned);
            GCHandle xFerBufferHandle = GCHandle.Alloc(xferBufs[0], GCHandleType.Pinned);
            GCHandle overlapDataHandle = GCHandle.Alloc(ovLaps[0], GCHandleType.Pinned);
            GCHandle pktsInfoHandle = GCHandle.Alloc(pktsInfo[0], GCHandleType.Pinned);

            // Reset the Decoder
            //_decoder = new CellPMTDataDecoder();
            //_decoder.CellPMTDataAvailableEventHandler += Decoder_PMTDataAvailable;
            try
            {
                _mcu.LockNLoad(cmdBufs, xferBufs, ovLaps, pktsInfo);
            }
            catch (NullReferenceException ex)
            {
                // This exception gets thrown if the device is unplugged 
                // while we're streaming data
                Console.WriteLine($"Data Streaming Interrupted.  Was the device unplugged?{Environment.NewLine}");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            //Release the pinned memory and make it available to GC
            cmdBufferHandle.Free();
            xFerBufferHandle.Free();
            overlapDataHandle.Free();
            pktsInfoHandle.Free();
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

        static async void PacketAvailable(object sender, EventArgs e)
        {
            bool success;
            byte[] data;
            //while (_mcu.packetManager.DataAvailable())
            //{
            (success, data) = _mcu.packetManager.GetNextPacket();
            if (success) { await Task.Run(() => AnalyzePacket(data)); }
            Console.WriteLine(data);
            //}
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
