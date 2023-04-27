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

        //Cypress PSOC MCU
        static private CyUSBDevice _device;
        static private USBDeviceList _usbDevices;
        static private CyUSBEndPoint _baseControlEndpoint;
        static private CyControlEndPoint _controlEndpoint;
        static private CyUSBEndPoint _baseIsoEndpoint;
        static private CyIsocEndPoint _isoEndpoint;
        static private int _isoPacketBlockSize;
        static private CellPMTDataDecoder _decoder;
        static private bool _acquireData;
        static private List<byte> prevPacket = new List<byte>() { 0x00, 0x00, 0x00, 0x00 };
        static private Queue<byte[]> _packets;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(App_ProcessExit);
            System.Diagnostics.Process myProcess = System.Diagnostics.Process.GetCurrentProcess();
            myProcess.PriorityClass = System.Diagnostics.ProcessPriorityClass.High;
            InitializeCy();
            StopPMT();
            Thread.Sleep(50);
            start();
        }

        static void App_ProcessExit(object sender, EventArgs e)
        {
            StopPMT();
            //Thread.Sleep(1000000000);
            //Environment.Exit(0);
        }

        static private void InitializeCy()
        {
            _usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);
            _device = _usbDevices[0] as CyUSBDevice;

            _baseControlEndpoint = _device.ControlEndPt;
            _controlEndpoint = _baseControlEndpoint as CyControlEndPoint;

            _baseIsoEndpoint = _device.IsocInEndPt;
            _baseIsoEndpoint.XferSize = 512;
            _isoPacketBlockSize = (_baseIsoEndpoint as CyIsocEndPoint).GetPktBlockSize(512); //Set Bytes to transfer from device
            _isoEndpoint = _baseIsoEndpoint as CyIsocEndPoint;

            _usbDevices.DeviceRemoved += new EventHandler(usbDevices_DeviceRemoved);
            _usbDevices.DeviceAttached += new EventHandler(usbDevices_DeviceAttached);
        }

        //Event handler for new device attach     
        static void usbDevices_DeviceAttached(object sender, EventArgs e)
        {
            USBEventArgs usbEvent = e as USBEventArgs;
            _usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB); //reinitialize
            _device = _usbDevices[0] as CyUSBDevice;
        }


        //Event handler for device removal       
        static void usbDevices_DeviceRemoved(object sender, EventArgs e)
        {
            USBEventArgs usbEvent = e as USBEventArgs;
            _baseControlEndpoint = null; // reset
            _baseIsoEndpoint = null; // reset
        }

        static void start()
        {
            SetData(0x60, 0, 0, 2); //Set trigger as normal mode
            Thread.Sleep(50);
            SortControl(4);
            Thread.Sleep(50);
            SetData(0x30, 1, 1, 2);//start data acquisition
            Thread.Sleep(50);
            DataAcqusitionThread();
        }

        static void SortControl(int control)
        {
            SetData(0x6C, control, 0, 2); //control = 0: stop, 1: start, 2: pause, 3: resume
        }

        static void StopPMT()
        {
            SetData(0x30, 0, 1, 2);//stop data acquisition
            Thread.Sleep(50);
            _acquireData = false; //stop usb data transfer
        }
        static private void SetData(Int16 ReqCode, int wValue, int wIndex, int bytes)
        {
            int size = 0;
            byte[] buffer = new byte[0];

            if (_controlEndpoint != null)
            {
                _controlEndpoint.Target = CyConst.TGT_DEVICE;

                _controlEndpoint.ReqType = CyConst.REQ_VENDOR;

                _controlEndpoint.Direction = CyConst.DIR_TO_DEVICE;

                try
                {
                    _controlEndpoint.ReqCode = (byte)ReqCode; //Set request code
                    _controlEndpoint.Value = (ushort)wValue; //Set wValue
                    _controlEndpoint.Index = (ushort)wIndex; //Set wIndex
                }
                catch (Exception ex)
                {
                    string msg = ex.Message;
                    return;
                }

                _controlEndpoint.XferData(ref buffer, ref size);
            }
        }

        static void DataAcqusitionThread()
        {
            byte[] buffer = new byte[512];

            _acquireData = true; //Start usb data transfer

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
            _decoder = new CellPMTDataDecoder();
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

        static unsafe void LockNLoad(byte[] cBufs, byte[] xBufs, byte[] oLaps, ISO_PKT_INFO[] pktsInfo)
        {
            GCHandle bufSingleTransfer;
            GCHandle bufDataAllocation;
            GCHandle bufPktsInfo;
            GCHandle handleOverlap;


            // Allocate one set of buffers for the queue, Buffered IO method require user to allocate a buffer as a part of command buffer,
            // the BeginDataXfer does not allocated it. BeginDataXfer will copy the data from the main buffer to the allocated while initializing the commands.
            cBufs = new byte[CyConst.SINGLE_XFER_LEN + _isoPacketBlockSize + ((_baseIsoEndpoint.XferMode == XMODE.BUFFERED) ? 512 : 0)];

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
                _baseIsoEndpoint.BeginDataXfer(ref cBufs, ref xBufs, ref len, ref oLaps);

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

        static unsafe void XferData(byte[] cBufs, byte[] xBufs, byte[] oLaps, ISO_PKT_INFO[] pktsInfo, GCHandle handleOverlap)
        {
            int len = 0;

            CyUSB.OVERLAPPED ovData = new CyUSB.OVERLAPPED();

            while (_acquireData)
            {
                // WaitForXfer
                unsafe
                {

                    ovData = (CyUSB.OVERLAPPED)Marshal.PtrToStructure(handleOverlap.AddrOfPinnedObject(), typeof(CyUSB.OVERLAPPED));
                    if (!_baseIsoEndpoint.WaitForXfer(ovData.hEvent, 500))
                    {
                        _baseIsoEndpoint.Abort();
                        PInvoke.WaitForSingleObject(ovData.hEvent, 500);
                    }
                }

                // FinishDataXfer
                if (_isoEndpoint.FinishDataXfer(ref cBufs, ref xBufs, ref len, ref oLaps, ref pktsInfo))
                {
                    List<byte> currentEvent = new List<byte>();
                    currentEvent.Add(xBufs[0]);
                    currentEvent.Add(xBufs[1]);
                    currentEvent.Add(xBufs[2]);
                    currentEvent.Add(xBufs[3]);
                    int test = CellPMTDataDecoder.EventNumber(ref xBufs);

                    bool same = false;

                    for (int x = 0; x < currentEvent.Count; x++)
                    {
                        same = prevPacket[x] == currentEvent[x];
                        if (!same)
                            break;
                    }

                    ISO_PKT_INFO[] pkts = pktsInfo;

                    if (!same)
                    {
                        if (pkts[0].Status == 0)
                        {
                            GetPMTData(xBufs);
                        }
                        prevPacket = currentEvent;
                    }

                }


                // Re-submit this buffer into the queue
                len = 512;
                _baseIsoEndpoint.BeginDataXfer(ref cBufs, ref xBufs, ref len, ref oLaps);

            } // End infinite loop
            // Let's recall all the queued buffer and abort the end point.
            _baseIsoEndpoint.Abort();
        }

        static void GetPMTData(byte[] buf)

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
