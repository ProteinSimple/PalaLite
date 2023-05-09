using ExtensionMethods;
using System;
using System.IO;
using System.Collections.Generic;

namespace PalaLite.Models
{
    public class CellPMTDataDecoder
    {
        private enum ChannelFilter : int
        {
            FL11 = 0b100000000000,
            FL12 = 0b10000000000,
            FL13 = 0b1000000000,
            FL14 = 0b100000000,
            FL15 = 0b10000000,
            FL16 = 0b1000000,
            FL21 = 0b100000,
            FL22 = 0b10000,
            FL23 = 0b1000,
            FL24 = 0b100,
            FL25 = 0b10,
            FL26 = 0b1
        }

        private readonly int _dataMinimum = 1;
        private readonly int _dataMaximum = 200000;
        private int _firstEvent;
        private int _previousFirstEvent;
        private int _lastEvent;

        private readonly string _logPath;
        private string _rawEventCountFilename;

        private bool _logData;
        private bool _printConsoleOutput;

        public event EventHandler DoneEventHandler;
        public event EventHandler<DataAvailableEventArgs> DataAvailableEventHandler;

        private List<string> _logBuffer = new List<string>();
        private List<string> _consoleBuffer = new List<string>();

        private int _packetCounter = 0;
        private int _packetsBeforeSend = 500;
        private byte[] _previousPacket;
        private bool _printNextPacket = false;

        public CellPMTDataDecoder()
        {
            _logData = true;
            //_printConsoleOutput = true;
            _logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Namocell",
                "Logs",
                "Test");
            _rawEventCountFilename = $"{DateTime.Now.ToString("yyyyMMdd-hhmmss")}_PalaLite.csv";
            _packetCounter = 0;
            _logBuffer.Add("Event Number,Time (ms),Event String");
        }

        public void AnalyzePacket(byte[] packet, int selectedChannels, int channelNumber, int plateRow, int plateColumn)
        {
            _packetCounter += 1;
            CellPMTData cellPMTData = new CellPMTData();
            int numParameters;
            int channel;        //selected channels
            int bytesPerEvent;  //number of bytes per event
            int channelCounter; //count next byte
            string subPacket;

            //Data from each PMT is 4 bytes float
            byte[] Count = new byte[4];
            //Get event number
            Count[0] = packet[0];
            Count[1] = packet[1];
            Count[2] = packet[2];
            Count[3] = packet[3];

            byte[] Time = new byte[4]; //Ticks
            byte[] Sort = new byte[4]; 
            byte[] BL0W = new byte[4]; //laser 1 FSC width
            byte[] BL0 = new byte[4];  //laser 1 FSC height
            byte[] RL0 = new byte[4];  //laser 2 FSC height
            byte[] BL1 = new byte[4];  //laser 1 SSC height
            byte[] BL2 = new byte[4];  //laser 1 PMT 1
            byte[] BL3 = new byte[4];  //laser 1 PMT 2
            byte[] BL4 = new byte[4];  //laser 1 PMT 3
            byte[] BL5 = new byte[4];  //laser 1 PMT 4
            byte[] BL6 = new byte[4];  //laser 1 PMT 5
            byte[] BL7 = new byte[4];  //laser 1 PMT 6
            byte[] RL2 = new byte[4];  //laser 2 PMT 1
            byte[] RL3 = new byte[4];  //laser 2 PMT 2
            byte[] RL4 = new byte[4];  //laser 1 PMT 3
            byte[] RL5 = new byte[4];  //laser 1 PMT 4
            byte[] RL6 = new byte[4];  //laser 1 PMT 5
            byte[] RL7 = new byte[4];  //laser 1 PMT 6

            int currentEvent;
            int previousEvent = 0;

            numParameters = channelNumber + 7; //calculate the number of Parameters
            bytesPerEvent = numParameters * 4; //calculate the number of bytes per event

            _firstEvent = BitConverter.ToInt32(Count, 0);

            if (_firstEvent > 10000000)
                _firstEvent = 10000000;


            if (_firstEvent != _previousFirstEvent) // Skip if current event matches previous first event.
            {
                int eventIndex = 0;
                bool moreDataInPacket = true;

                _previousFirstEvent = _firstEvent;

                if (_printNextPacket)
                {
                    _logBuffer.Add($",,Next Packet:,{BitConverter.ToString(packet)}");
                }

                if ((Math.Abs(_firstEvent - _lastEvent) > 1) && (_lastEvent > 0) && !_printNextPacket)
                {
                    var msg = "**************************************** Missed/Bad Packet ****************************************";
                    if (_printConsoleOutput) { _consoleBuffer.Add(msg); }
                    if (_logData) { 
                        _logBuffer.Add(msg);
                        _logBuffer.Add($",,Previous Packet:,{BitConverter.ToString(_previousPacket)}");
                        _logBuffer.Add($",,Bad Packet:,{BitConverter.ToString(packet)}");
                        _printNextPacket = true;
                    }
                }
                else
                {
                    var msg = "======================================== New Packet ========================================";
                    if (_printConsoleOutput)
                    {
                        _consoleBuffer.Add(msg);
                        _consoleBuffer.Add(BitConverter.ToString(packet));
                    }
                    if (_logData) { _logBuffer.Add(msg); }
                    _printNextPacket = false;
                }

                // Repeat for each event in the packet
                while (moreDataInPacket)
                {
                    Count[0] = packet[0 + bytesPerEvent * eventIndex];
                    Count[1] = packet[1 + bytesPerEvent * eventIndex];
                    Count[2] = packet[2 + bytesPerEvent * eventIndex];
                    Count[3] = packet[3 + bytesPerEvent * eventIndex];

                    currentEvent = BitConverter.ToInt32(Count, 0);
                    if (currentEvent > 10000000)
                        currentEvent = 10000000;

                    if (currentEvent > previousEvent)
                    {
                        //Event Timestamp
                        Time[0] = packet[4 + bytesPerEvent * eventIndex];
                        Time[1] = packet[5 + bytesPerEvent * eventIndex];
                        Time[2] = packet[6 + bytesPerEvent * eventIndex];
                        Time[3] = packet[7 + bytesPerEvent * eventIndex];

                        Sort[0] = packet[8 + bytesPerEvent * eventIndex];
                        Sort[1] = packet[9 + bytesPerEvent * eventIndex];
                        Sort[2] = packet[10 + bytesPerEvent * eventIndex];
                        Sort[3] = packet[11 + bytesPerEvent * eventIndex];

                        BL0W[0] = packet[12 + bytesPerEvent * eventIndex];
                        BL0W[1] = packet[13 + bytesPerEvent * eventIndex];
                        BL0W[2] = packet[14 + bytesPerEvent * eventIndex];
                        BL0W[3] = packet[15 + bytesPerEvent * eventIndex];

                        BL0[0] = packet[16 + bytesPerEvent * eventIndex];
                        BL0[1] = packet[17 + bytesPerEvent * eventIndex];
                        BL0[2] = packet[18 + bytesPerEvent * eventIndex];
                        BL0[3] = packet[19 + bytesPerEvent * eventIndex];

                        RL0[0] = packet[20 + bytesPerEvent * eventIndex];
                        RL0[1] = packet[21 + bytesPerEvent * eventIndex];
                        RL0[2] = packet[22 + bytesPerEvent * eventIndex];
                        RL0[3] = packet[23 + bytesPerEvent * eventIndex];

                        BL1[0] = packet[24 + bytesPerEvent * eventIndex];
                        BL1[1] = packet[25 + bytesPerEvent * eventIndex];
                        BL1[2] = packet[26 + bytesPerEvent * eventIndex];
                        BL1[3] = packet[27 + bytesPerEvent * eventIndex];

                        channelCounter = 28;        //reset buffer index
                        channel = selectedChannels; //reset channel selection

                        subPacket = string.Format("{0}-{1}-{2}-{3}-{4}-{5}-{6}",
                            BitConverter.ToString(Count),
                            BitConverter.ToString(Time),
                            BitConverter.ToString(Sort),
                            BitConverter.ToString(BL0W),
                            BitConverter.ToString(BL0),
                            BitConverter.ToString(RL0),
                            BitConverter.ToString(BL1));

                        if (Analyze(ChannelFilter.FL11, eventIndex, ref BL2))
                        {
                            cellPMTData.FL11 = Utilities.Clamp(BitConverter.ToSingle(BL2, 0), _dataMinimum, _dataMaximum);
                        }

                        if (Analyze(ChannelFilter.FL12, eventIndex, ref BL3))
                        {
                            cellPMTData.FL12 = Utilities.Clamp(BitConverter.ToSingle(BL3, 0), _dataMinimum, _dataMaximum);
                        }

                        if (Analyze(ChannelFilter.FL13, eventIndex, ref BL4))
                        {
                            cellPMTData.FL13 = Utilities.Clamp(BitConverter.ToSingle(BL4, 0), _dataMinimum, _dataMaximum);
                        }

                        if (Analyze(ChannelFilter.FL14, eventIndex, ref BL5))
                        {
                            cellPMTData.FL14 = Utilities.Clamp(BitConverter.ToSingle(BL5, 0), _dataMinimum, _dataMaximum);
                        }

                        if (Analyze(ChannelFilter.FL15, eventIndex, ref BL6))
                        {
                            cellPMTData.FL15 = Utilities.Clamp(BitConverter.ToSingle(BL6, 0), _dataMinimum, _dataMaximum);
                        }

                        if (Analyze(ChannelFilter.FL16, eventIndex, ref BL7))
                        {
                            cellPMTData.FL16 = Utilities.Clamp(BitConverter.ToSingle(BL7, 0), _dataMinimum, _dataMaximum);
                        }

                        if (Analyze(ChannelFilter.FL21, eventIndex, ref RL2))
                        {
                            cellPMTData.FL21 = Utilities.Clamp(BitConverter.ToSingle(RL2, 0), _dataMinimum, _dataMaximum);
                        }

                        if (Analyze(ChannelFilter.FL22, eventIndex, ref RL3))
                        {
                            cellPMTData.FL22 = Utilities.Clamp(BitConverter.ToSingle(RL3, 0), _dataMinimum, _dataMaximum);
                        }

                        if (Analyze(ChannelFilter.FL23, eventIndex, ref RL4))
                        {
                            cellPMTData.FL23 = Utilities.Clamp(BitConverter.ToSingle(RL4, 0), _dataMinimum, _dataMaximum);
                        }

                        if (Analyze(ChannelFilter.FL24, eventIndex, ref RL5))
                        {
                            cellPMTData.FL24 = Utilities.Clamp(BitConverter.ToSingle(RL5, 0), _dataMinimum, _dataMaximum);
                        }

                        if (Analyze(ChannelFilter.FL25, eventIndex, ref RL6))
                        {
                            cellPMTData.FL25 = Utilities.Clamp(BitConverter.ToSingle(RL6, 0), _dataMinimum, _dataMaximum);
                        }

                        if (Analyze(ChannelFilter.FL26, eventIndex, ref RL7))
                        {
                            cellPMTData.FL26 = Utilities.Clamp(BitConverter.ToSingle(RL7, 0), _dataMinimum, _dataMaximum);
                        }


                        cellPMTData.EventCount = currentEvent;
                        cellPMTData.Time = BitConverter.ToUInt32(Time, 0);
                        cellPMTData.Sort = BitConverter.ToInt32(Sort, 0);
                        //cellData.FSC1W = Utilities.Clamp(BitConverter.ToSingle(BL0W, 0), _dataMinimum, _dataMaximum);
                        cellPMTData.FSC1W = BitConverter.ToSingle(BL0W, 0);
                        cellPMTData.FSC1H = Utilities.Clamp(BitConverter.ToSingle(BL0, 0), _dataMinimum, _dataMaximum);
                        //cellData.FSC2H = Utilities.Clamp(BitConverter.ToSingle(RL0, 0), _dataMinimum, _dataMaximum);
                        cellPMTData.FSC2H = BitConverter.ToSingle(RL0, 0);
                        cellPMTData.SSC1H = Utilities.Clamp(BitConverter.ToSingle(BL1, 0), _dataMinimum, _dataMaximum);

                        cellPMTData.Row = CNM_Def.STRROW_Alphabet[plateRow];
                        cellPMTData.Column = (plateColumn + 1).ToString();

                        if (currentEvent < 0)
                            return;

                        //DataAvailable(cellPMTData);

                        if (_logData) {  _logBuffer.Add($"{cellPMTData.ToString()},{subPacket}"); }

                        if (_printConsoleOutput)
                        {
                            _consoleBuffer.Add(currentEvent.ToString());
                            _consoleBuffer.Add(subPacket);
                        }

                        eventIndex++;
                        if (eventIndex * bytesPerEvent > 450)
                            moreDataInPacket = false;

                        previousEvent = currentEvent;
                    }
                    else //no more data in the packet.
                    {
                        moreDataInPacket = false;
                        _lastEvent = previousEvent;
                        _previousPacket = (byte[])packet.Clone();
                        SendConsoleData();
                    }
                }
            }

            bool Analyze(ChannelFilter filter, int index, ref byte[] target)
            {
                bool dataFound = channel >= (int)filter;

                if (dataFound)
                {
                    channel -= (int)filter;
                    target[0] = packet[channelCounter + (bytesPerEvent * index)];
                    target[1] = packet[channelCounter + 1 + (bytesPerEvent * index)];
                    target[2] = packet[channelCounter + 2 + (bytesPerEvent * index)];
                    target[3] = packet[channelCounter + 3 + (bytesPerEvent * index)];
                    channelCounter += 4;
                    subPacket += BitConverter.ToString(target) + "-";
                }
                return dataFound;
            }
        }

        /// <summary>
        /// Get the event number of an event sub-packet.
        /// </summary>
        /// <param name="data"></param>
        /// <returns> The Int32 representation of the first four bytes in the referenced byte array</returns>
        public static int EventNumber(byte[] data)
        {
            int bytesPerValue = 4;
            byte[] target = new byte[bytesPerValue];
            int eventNumber = 0;

            if (data.Length >= bytesPerValue)
            { 
                for (int i = 0; i < bytesPerValue; i++)
                {
                    target[i] = data[i];
                }
                eventNumber = BitConverter.ToInt32(target, 0);
            }

            return eventNumber;
        }

        public void SendConsoleData()
        {
            if (_printConsoleOutput)
            {
                foreach (string val in _consoleBuffer)
                {
                    Console.WriteLine(val);
                }
                _consoleBuffer.Clear();
            }
        }
        public void ExportData()
        {
            if (_logData)
            {
                File.WriteAllLines
                (
                    Path.Combine(_logPath, _rawEventCountFilename),
                    _logBuffer
                );
                _logBuffer.Clear();
            }
            _packetCounter = 0;
        }

        private void DataAvailable(CellPMTData cellPMTData)
        {
            DataAvailableEventArgs args = new DataAvailableEventArgs();
            args.CellPMTData = cellPMTData;
            OnDataAvailable(args);
        }

        private void Done()
        {
            OnDone(new EventArgs());
            ExportData();
            Reset();
        }

        protected virtual void OnDone(EventArgs e)
        {
            EventHandler handler = DoneEventHandler;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnDataAvailable(DataAvailableEventArgs e)
        {
            EventHandler<DataAvailableEventArgs> handler = DataAvailableEventHandler;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public void Reset()
        {
            _firstEvent = 0;
            _previousFirstEvent = 0;
            _rawEventCountFilename = $"{DateTime.Now.ToString("yyyyMMdd-hhmmss")}_PalaLite.csv";
            _packetCounter = 0;
        }
    }

    public class DataAvailableEventArgs : EventArgs
    {
        public CellPMTData CellPMTData { get; set; }
    }
}