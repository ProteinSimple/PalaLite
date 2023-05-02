﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
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
            /*try
            {
                DataAcqusitionThread();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.StackTrace);
            }*/
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
            //}
        }

        static void PacketLimitReached(object sender, EventArgs e)
        {
            // Stop Data Streaming
            _mcu.StopPMT();
            while(_mcu.packetManager.DataAvailable())
            {
                // Wait.
            }
            _decoder.ExportData();
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
