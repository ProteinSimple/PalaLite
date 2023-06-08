using CyUSB;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace PalaLite.Models
{
    //Cypress PSOC MCU
    internal class MicroController
    {
        public CyUSBDevice device;
        public USBDeviceList usbDevices;
        public CyUSBEndPoint baseControlEndpoint;
        public CyControlEndPoint controlEndpoint;
        public CyUSBEndPoint baseIsoEndpoint;
        public CyIsocEndPoint isoEndpoint;
        public int isoPacketBlockSize;

        public bool acquireData;
        public int previousFirstEvent;
        public int currentFirstEvent;

        public PacketManager packetManager;

        public event EventHandler StartDataAcquisitionEventHandler;

        private static int _PACKET_SIZE = 512;

        public MicroController() 
        {
            packetManager = new PacketManager();
            InitializeCy();
            //StopPMT();
            Console.WriteLine($"Successfully connected to simulator.{Environment.NewLine}");
        }


        private void InitializeCy()
        {
            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);
            device = usbDevices[0] as CyUSBDevice;

            baseControlEndpoint = device.ControlEndPt;
            controlEndpoint = baseControlEndpoint as CyControlEndPoint;

            baseIsoEndpoint = device.IsocInEndPt;
            baseIsoEndpoint.XferSize = _PACKET_SIZE;
            isoPacketBlockSize = (baseIsoEndpoint as CyIsocEndPoint).GetPktBlockSize(_PACKET_SIZE); //Set Bytes to transfer from device
            isoEndpoint = baseIsoEndpoint as CyIsocEndPoint;

            usbDevices.DeviceRemoved += new EventHandler(usbDevices_DeviceRemoved);
            usbDevices.DeviceAttached += new EventHandler(usbDevices_DeviceAttached);
        }

        //Event handler for new device attach     
        private void usbDevices_DeviceAttached(object sender, EventArgs e)
        {
            USBEventArgs usbEvent = e as USBEventArgs;
            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB); //reinitialize
            device = usbDevices[0] as CyUSBDevice;
        }

        //Event handler for device removal       
        private void usbDevices_DeviceRemoved(object sender, EventArgs e)
        {
            USBEventArgs usbEvent = e as USBEventArgs;
            baseControlEndpoint = null; // reset
            baseIsoEndpoint = null; // reset
        }

        private void SortControl(int control)
        {
            SetData(0x6C, control, 0, 2); //control = 0: stop, 1: start, 2: pause, 3: resume
        }

        /*private void StartDataTransfer()
        {
            Thread DataAcqusition = new Thread(new ThreadStart(DataAcqusitionThread)); //kick off a new thread
            DataAcqusition.IsBackground = true;
            DataAcqusition.Priority = ThreadPriority.Highest;
            DataAcqusition.Start();
        }*/

        private unsafe void XferData(byte[] cBufs, byte[] xBufs, byte[] oLaps, ISO_PKT_INFO[] pktsInfo, GCHandle handleOverlap)
        {
            int len = 0;

            OVERLAPPED ovData = new OVERLAPPED();

            while ( acquireData)
            {
                // WaitForXfer
                unsafe
                {

                    ovData = (OVERLAPPED)Marshal.PtrToStructure(handleOverlap.AddrOfPinnedObject(), typeof(OVERLAPPED));
                    if (!   baseIsoEndpoint.WaitForXfer(ovData.hEvent, 500))
                    {
                        baseIsoEndpoint.Abort();
                        PInvoke.WaitForSingleObject(ovData.hEvent, 500);
                    }
                }

                // FinishDataXfer
                if (isoEndpoint.FinishDataXfer(ref cBufs, ref xBufs, ref len, ref oLaps, ref pktsInfo))
                {
                    currentFirstEvent = CellPMTDataDecoder.EventNumber(xBufs);

                    ISO_PKT_INFO[] pkts = pktsInfo;

                    if ((currentFirstEvent != previousFirstEvent) &&  currentFirstEvent > 0)
                    {
                        if (pkts[0].Status == 0)
                        {
                            packetManager.Add(xBufs);
                        }
                        previousFirstEvent = currentFirstEvent;
                    }
                }
                // Re-submit this buffer into the queue
                len = _PACKET_SIZE;
                    baseIsoEndpoint.BeginDataXfer(ref cBufs, ref xBufs, ref len, ref oLaps);

            } // End infinite loop
              // Let's recall all the queued buffer and abort the end point.
                baseIsoEndpoint.Abort();
        }

        private unsafe void LockNLoad(byte[] cBufs, byte[] xBufs, byte[] oLaps, ISO_PKT_INFO[] pktsInfo)
        {
            GCHandle bufSingleTransfer;
            GCHandle bufDataAllocation;
            GCHandle bufPktsInfo;
            GCHandle handleOverlap;


            // Allocate one set of buffers for the queue, Buffered IO method require user to allocate a buffer as a part of command buffer,
            // the BeginDataXfer does not allocated it. BeginDataXfer will copy the data from the main buffer to the allocated while initializing the commands.
            cBufs = new byte[CyConst.SINGLE_XFER_LEN +  isoPacketBlockSize + (( baseIsoEndpoint.XferMode == XMODE.BUFFERED) ? _PACKET_SIZE : 0)];

            xBufs = new byte[_PACKET_SIZE];

            //initialize the buffer with initial value 0xA5
            for (int iIndex = 0; iIndex < _PACKET_SIZE; iIndex++)
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

                int len = _PACKET_SIZE;
                baseIsoEndpoint.BeginDataXfer(ref cBufs, ref xBufs, ref len, ref oLaps);

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

        public void StartPMT(int numPackets)
        {
            StopPMT();
            Thread.Sleep(50);
            packetManager.PacketsToAnalyze = numPackets;
            SetData(0x60, 0, 0, 2); //Set trigger as normal mode
            Thread.Sleep(50);

            PrepDataAcq();
            SetData(0x3F, 0, 0, 2);//set sort target number to 0 to disable sorting
            Thread.Sleep(50);

            SortControl(4);
            Thread.Sleep(50);
            SetData(0x30, 1, 1, 2); //start data acquisition
            Thread.Sleep(50);
            Console.WriteLine($"Beginning Data acquisition of {packetManager.PacketsToAnalyze} packets.{Environment.NewLine}");
            StartDataAcquisition();
        }

        private void PrepDataAcq()
        {
            SetPMT();
            Thread.Sleep(50);
        }

        private void SetPMT()
        {
            SetData(0x30, 0, 1, 2);//stop data acquisition
            Thread.Sleep(50);
            SetXferDataStructure(); //Set custom data format
            Thread.Sleep(100);
            SetGain();
            Thread.Sleep(100);
        }

        private void SetXferDataStructure()
        {
            int bytes = 8;
            byte[] buffer = new byte[bytes];
            bool bXferCompleted = false;

            //laser 1 FSC width
            buffer[0] = 0x00;
            buffer[1] = 0x02;

            //laser 1 FSC height
            buffer[2] = 0x00;
            buffer[3] = 0x00;

            //laser 2 FSC height
            buffer[4] = 0x0A;
            buffer[5] = 0x00;

            //laser 1 SSC height
            buffer[6] = 0x01;
            buffer[7] = 0x00;

            if (controlEndpoint != null)
            {
                controlEndpoint.Target = CyConst.TGT_DEVICE;
                controlEndpoint.ReqType = CyConst.REQ_VENDOR;
                controlEndpoint.Direction = CyConst.DIR_TO_DEVICE;

                try
                {
                    controlEndpoint.ReqCode = 0x67; //Set request code for setting transfer data structure
                    controlEndpoint.Value = 0; //Set wValue
                    controlEndpoint.Index = 1; //Set wIndex
                }
                catch (Exception ex)
                {
                    string msg = ex.Message;
                    return;
                }

                bXferCompleted = controlEndpoint.XferData(ref buffer, ref bytes);
            }
        }

        private void SetGain()
        {
            SetData(0x40, 600 * 26, 0, 2); //set FSC gain channel 0. wValue=0-0xFFFF (16-bit)=0 to 2500 mV
            Thread.Sleep(50);
            SetData(0x41, 500 * 26, 0, 2); //set SSC gain channel 0. wValue=0-0xFFFF (16-bit)=0 to 2500 mV
            Thread.Sleep(50);
            SetData(0x42, 475 * 53, 0, 2); //set PMT1 gain channel 0. wValue=0-0xFFFF (16-bit)=0 to 1200 mV
            Thread.Sleep(50);
            SetData(0x43, 525 * 53, 0, 2); //set PMT2 gain channel 0. wValue=0-0xFFFF (16-bit)=0 to 1200 mV
            Thread.Sleep(50);
            SetData(0x44, 575 * 53, 0, 2); //set PMT3 gain channel 0. wValue=0-0xFFFF (16-bit)=0 to 1200 mV
            Thread.Sleep(50);
            SetData(0x45, 475 * 53, 0, 2); //set PMT4 gain channel 0. wValue=0-0xFFFF (16-bit)=0 to 1200 mV
            Thread.Sleep(50);
            SetData(0x46, 600 * 53, 0, 2); //set PMT5 gain channel 0. wValue=0-0xFFFF (16-bit)=0 to 1200 mV
            Thread.Sleep(50);
            SetData(0x47, 0 * 53, 0, 2); //set PMT6 gain channel 0. wValue=0-0xFFFF (16-bit)=0 to 1200 mV
            Thread.Sleep(50);
        }

        private void StartDataAcquisition()
        {
            DataAcquisitionThread();
        }

        protected virtual void OnStartDataAcquisition(EventArgs e)
        {
            EventHandler handler = StartDataAcquisitionEventHandler;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void DataAcquisitionThread()
        {
            byte[] buffer = new byte[_PACKET_SIZE];

            acquireData = true; //Start usb data transfer

            // Setup iso-transfer buffers size 512 and one packet per transfer
            byte[] cmdBufs = new byte[_PACKET_SIZE];
            byte[] xferBufs = new byte[_PACKET_SIZE];
            byte[] ovLaps = new byte[_PACKET_SIZE];
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

        public void StopPMT()
        {
            SetData(0x30, 0, 1, 2);//stop data acquisition
            Thread.Sleep(50);
            acquireData = false; //stop usb data transfer
        }

        public void SetData(Int16 ReqCode, int wValue, int wIndex, int bytes)
        {
            int size = 0;
            byte[] buffer = new byte[0];

            if (controlEndpoint != null)
            {
                controlEndpoint.Target = CyConst.TGT_DEVICE;

                controlEndpoint.ReqType = CyConst.REQ_VENDOR;

                controlEndpoint.Direction = CyConst.DIR_TO_DEVICE;

                try
                {
                    controlEndpoint.ReqCode = (byte)ReqCode; //Set request code
                    controlEndpoint.Value = (ushort)wValue;  //Set wValue
                    controlEndpoint.Index = (ushort)wIndex;  //Set wIndex
                }
                catch (Exception ex)
                {
                    string msg = ex.Message + ex.StackTrace;
                    Console.WriteLine(msg);
                    StopPMT();
                    return;
                }

                controlEndpoint.XferData(ref buffer, ref size);
            }
        }
    }
}
