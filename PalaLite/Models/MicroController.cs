using CyUSB;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace PalaLite.Models
{
    //Cypress PSOC MCU
    internal class MicroController
    {
        private CyUSBDevice _device;
        private USBDeviceList _usbDevices;
        private CyUSBEndPoint _baseControlEndpoint;
        private CyControlEndPoint _controlEndpoint;
        private CyUSBEndPoint _baseIsoEndpoint;
        private CyIsocEndPoint _isoEndpoint;
        private int _isoPacketBlockSize;

        private bool _acquireData;
        private int _previousFirstEvent;
        private int _currentFirstEvent;

        public PacketManager packetManager;

        public event EventHandler StartDataAcquisitionEventHandler;

        public MicroController() 
        {
            packetManager = new PacketManager();
            InitializeCy();
            StopPMT();
        }


        private void InitializeCy()
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
        private void usbDevices_DeviceAttached(object sender, EventArgs e)
        {
            USBEventArgs usbEvent = e as USBEventArgs;
            _usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB); //reinitialize
            _device = _usbDevices[0] as CyUSBDevice;
        }

        //Event handler for device removal       
        private void usbDevices_DeviceRemoved(object sender, EventArgs e)
        {
            USBEventArgs usbEvent = e as USBEventArgs;
            _baseControlEndpoint = null; // reset
            _baseIsoEndpoint = null; // reset
        }

        private unsafe void XferData(byte[] cBufs, byte[] xBufs, byte[] oLaps, ISO_PKT_INFO[] pktsInfo, GCHandle handleOverlap)
        {
            int len = 0;

            OVERLAPPED ovData = new OVERLAPPED();

            while (_acquireData)
            {
                // WaitForXfer
                unsafe
                {

                    ovData = (OVERLAPPED)Marshal.PtrToStructure(handleOverlap.AddrOfPinnedObject(), typeof(OVERLAPPED));
                    if (!_baseIsoEndpoint.WaitForXfer(ovData.hEvent, 500))
                    {
                        _baseIsoEndpoint.Abort();
                        PInvoke.WaitForSingleObject(ovData.hEvent, 500);
                    }
                }

                // FinishDataXfer
                if (_isoEndpoint.FinishDataXfer(ref cBufs, ref xBufs, ref len, ref oLaps, ref pktsInfo))
                {
                    _currentFirstEvent = CellPMTDataDecoder.EventNumber(xBufs);

                    ISO_PKT_INFO[] pkts = pktsInfo;

                    if ((_previousFirstEvent != _currentFirstEvent) && _currentFirstEvent > 0)
                    {
                        if (pkts[0].Status == 0)
                        {
                            packetManager.Add(xBufs);
                        }
                        _previousFirstEvent = _currentFirstEvent;
                    }
                }
                // Re-submit this buffer into the queue
                len = 512;
                _baseIsoEndpoint.BeginDataXfer(ref cBufs, ref xBufs, ref len, ref oLaps);

            } // End infinite loop
              // Let's recall all the queued buffer and abort the end point.
            _baseIsoEndpoint.Abort();
        }

        public unsafe void LockNLoad(byte[] cBufs, byte[] xBufs, byte[] oLaps, ISO_PKT_INFO[] pktsInfo)
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

        public void StartPMT()
        {
            SetData(0x60, 0, 0, 2); //Set trigger as normal mode
            Thread.Sleep(50);
            SortControl(4);
            Thread.Sleep(50);
            SetData(0x30, 1, 1, 2); //start data acquisition
            Thread.Sleep(50);
            //StartDataTransfer();
            StartDataAcquisition();
            //DataAcqusitionThread();
        }

        private void StartDataAcquisition()
        {
            _acquireData = true;
            OnStartDataAcquisition(new EventArgs());
        }

        protected virtual void OnStartDataAcquisition(EventArgs e)
        {
            EventHandler handler = StartDataAcquisitionEventHandler;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public void StopPMT()
        {
            SetData(0x30, 0, 1, 2);//stop data acquisition
            Thread.Sleep(50);
            _acquireData = false; //stop usb data transfer
        }

        public void SetData(Int16 ReqCode, int wValue, int wIndex, int bytes)
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
                    _controlEndpoint.Value = (ushort)wValue;  //Set wValue
                    _controlEndpoint.Index = (ushort)wIndex;  //Set wIndex
                }
                catch (Exception ex)
                {
                    string msg = ex.Message + ex.StackTrace;
                    Console.WriteLine(msg);
                    StopPMT();
                    return;
                }

                _controlEndpoint.XferData(ref buffer, ref size);
            }
        }
    }
}
