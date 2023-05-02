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
                LockNLoad(cmdBufs, xferBufs, ovLaps, pktsInfo);
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

        static unsafe void XferData(byte[] cBufs, byte[] xBufs, byte[] oLaps, ISO_PKT_INFO[] pktsInfo, GCHandle handleOverlap)
        {
            int len = 0;

            OVERLAPPED ovData = new OVERLAPPED();

            while (_mcu.acquireData)
            {
                // WaitForXfer
                unsafe
                {

                    ovData = (OVERLAPPED)Marshal.PtrToStructure(handleOverlap.AddrOfPinnedObject(), typeof(OVERLAPPED));
                    if (!_mcu.baseIsoEndpoint.WaitForXfer(ovData.hEvent, 500))
                    {
                        _mcu.baseIsoEndpoint.Abort();
                        PInvoke.WaitForSingleObject(ovData.hEvent, 500);
                    }
                }

                // FinishDataXfer
                if (_mcu.isoEndpoint.FinishDataXfer(ref cBufs, ref xBufs, ref len, ref oLaps, ref pktsInfo))
                {
                    _mcu.currentFirstEvent = CellPMTDataDecoder.EventNumber(xBufs);

                    ISO_PKT_INFO[] pkts = pktsInfo;

                    if ((_mcu.previousFirstEvent != _mcu.currentFirstEvent) && _mcu.currentFirstEvent > 0)
                    {
                        if (pkts[0].Status == 0)
                        {
                            _mcu.packetManager.Add(xBufs);
                        }
                        _mcu.previousFirstEvent = _mcu.currentFirstEvent;
                    }
                }
                // Re-submit this buffer into the queue
                len = 512;
                _mcu.baseIsoEndpoint.BeginDataXfer(ref cBufs, ref xBufs, ref len, ref oLaps);

            } // End infinite loop
              // Let's recall all the queued buffer and abort the end point.
            _mcu.baseIsoEndpoint.Abort();
        }

        static unsafe void LockNLoad(byte[] cBufs, byte[] xBufs, byte[] oLaps, ISO_PKT_INFO[] pktsInfo)
        {
            GCHandle bufSingleTransfer;
            GCHandle bufDataAllocation;
            GCHandle bufPktsInfo;
            GCHandle handleOverlap;


            // Allocate one set of buffers for the queue, Buffered IO method require user to allocate a buffer as a part of command buffer,
            // the BeginDataXfer does not allocated it. BeginDataXfer will copy the data from the main buffer to the allocated while initializing the commands.
            cBufs = new byte[CyConst.SINGLE_XFER_LEN + _mcu.isoPacketBlockSize + ((_mcu.baseIsoEndpoint.XferMode == XMODE.BUFFERED) ? 512 : 0)];

            xBufs = new byte[512];

            //initialize the buffer with initial value 0xA5
            for (int iIndex = 0; iIndex < 512; iIndex++)
                xBufs[iIndex] = 0xA5;

            int sz = Math.Max(CyConst.OverlapSignalAllocSize, sizeof(OVERLAPPED));
            oLaps = new byte[sz];
            ISO_PKT_INFO[] Iskpt = new ISO_PKT_INFO[1];

            /*/////////////////////////////////////////////////////////////////////////////
             * 
             * Solution  for Variable Pinning:
             * Its expected that application pin memory before passing the variable address to the
             * library and subsequently to the windows driver.
             * 
             * Cypress Windows Driver is using this very same memory location for data reception or
             * data delivery to the device.
             * And, hence .Net Garbage collector isn't expected to move the memory location. And,
             * Pinning the memory location is essential. And, not through FIXED keyword, because of 
             * non-usability of temporary variable.
             * 
            /////////////////////////////////////////////////////////////////////////////*/

            bufSingleTransfer = GCHandle.Alloc(cBufs, GCHandleType.Pinned);
            bufDataAllocation = GCHandle.Alloc(xBufs, GCHandleType.Pinned);
            bufPktsInfo = GCHandle.Alloc(pktsInfo, GCHandleType.Pinned);
            handleOverlap = GCHandle.Alloc(oLaps, GCHandleType.Pinned);

            unsafe
            {
                CyUSB.OVERLAPPED ovLapStatus = new CyUSB.OVERLAPPED();
                ovLapStatus = (CyUSB.OVERLAPPED)Marshal.PtrToStructure(handleOverlap.AddrOfPinnedObject(), typeof(CyUSB.OVERLAPPED));
                ovLapStatus.hEvent = (IntPtr)PInvoke.CreateEvent(0, 0, 0, 0);
                Marshal.StructureToPtr(ovLapStatus, handleOverlap.AddrOfPinnedObject(), true);

                int len = 512;
                _mcu.baseIsoEndpoint.BeginDataXfer(ref cBufs, ref xBufs, ref len, ref oLaps);

            }


            XferData(cBufs, xBufs, oLaps, pktsInfo, handleOverlap); // All loaded. Let's go!

            unsafe
            {
                CyUSB.OVERLAPPED ovLapStatus = new CyUSB.OVERLAPPED();
                ovLapStatus = (CyUSB.OVERLAPPED)Marshal.PtrToStructure(handleOverlap.AddrOfPinnedObject(), typeof(CyUSB.OVERLAPPED));
                PInvoke.CloseHandle(ovLapStatus.hEvent);

                //Release the pinned allocation handles.     
                bufSingleTransfer.Free();
                bufDataAllocation.Free();
                bufPktsInfo.Free();
                handleOverlap.Free();

                cBufs = null;
                xBufs = null;
                oLaps = null;

            }
            GC.Collect();
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
