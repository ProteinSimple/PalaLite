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

        public MicroController() 
        {
            packetManager = new PacketManager();
            InitializeCy();
            StopPMT();
        }


        private void InitializeCy()
        {
            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);
            device = usbDevices[0] as CyUSBDevice;

            baseControlEndpoint = device.ControlEndPt;
            controlEndpoint = baseControlEndpoint as CyControlEndPoint;

            baseIsoEndpoint = device.IsocInEndPt;
            baseIsoEndpoint.XferSize = 512;
            isoPacketBlockSize = (baseIsoEndpoint as CyIsocEndPoint).GetPktBlockSize(512); //Set Bytes to transfer from device
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

        public void StartPMT()
        {
            SetData(0x60, 0, 0, 2); //Set trigger as normal mode
            Thread.Sleep(50);
            SortControl(4);
            Thread.Sleep(50);
            SetData(0x30, 1, 1, 2); //start data acquisition
            Thread.Sleep(50);
            StartDataAcquisition();
        }

        private void StartDataAcquisition()
        {
            acquireData = true;
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
