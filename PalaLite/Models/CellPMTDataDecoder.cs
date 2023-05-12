using ExtensionMethods;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

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
        private int _chunkSize = 4;
        private List<byte> _previousPacket;
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

        public List<PmtDataModel> DecodeRawPacket(List<byte> packet, int selectedChannels, int numExtraParameters, int plateRow, int plateColumn)
        {
            List<PmtDataModel> eventList = new List<PmtDataModel>();
            PmtDataModel currentData;
            int channelsToAnalyze;        //selected channels
            int packetIndex = 0; //count next byte
            string subPacket;

            int currentEvent;
            int previousEvent = 0;

            int numParameters = 7 + numExtraParameters; //calculate the number of total number of Parameters
            int bytesPerEvent = _chunkSize * numParameters; //calculate the number of bytes per event

            _firstEvent = BitConverter.ToInt32(packet.GetRange(0, 4).ToArray(), 0);

            if (_firstEvent > 10000000) // Do something to filter outliers here.
            {
                _firstEvent = 10000000;
            }

            if (_firstEvent != _previousFirstEvent) // Skip if current event matches previous first event.
            {
                bool searchPacket = true;

                _previousFirstEvent = _firstEvent;

                if (_printNextPacket)
                {
                    _logBuffer.Add($",,Next Packet:,{BitConverter.ToString(packet.ToArray())}");
                }

                if ((Math.Abs(_firstEvent - _lastEvent) > 1) && (_lastEvent > 0) && !_printNextPacket)
                {
                    var msg = "**************************************** Missed/Bad Packet ****************************************";
                    if (_printConsoleOutput) { _consoleBuffer.Add(msg); }
                    if (_logData)
                    {
                        _logBuffer.Add(msg);
                        _logBuffer.Add($",,Previous Packet:,{BitConverter.ToString(_previousPacket.ToArray())}");
                        _logBuffer.Add($",,Bad Packet:,{BitConverter.ToString(packet.ToArray())}");
                        _printNextPacket = true;
                    }
                }
                else
                {
                    var msg = "======================================== New Packet ========================================";
                    if (_printConsoleOutput)
                    {
                        _consoleBuffer.Add(msg);
                        _consoleBuffer.Add(BitConverter.ToString(packet.ToArray()));
                    }
                    if (_logData) { _logBuffer.Add(msg); }
                    _printNextPacket = false;
                }

                // Repeat for each event in the packet
                while (searchPacket)
                {
                    currentData = new PmtDataModel();
                    List<byte> eventPacket = packet.GetRange(packetIndex, bytesPerEvent);
                    List<List<byte>> eventChunks = Utilities.Chunk(eventPacket, _chunkSize);
                    subPacket = string.Join("-", eventPacket.ToArray());

                    packetIndex = packetIndex + bytesPerEvent; // Starting index of next event.

                    currentEvent = BitConverter.ToInt32(eventChunks[0].ToArray(), 0);

                    if (currentEvent > 10000000)
                        currentEvent = 10000000;

                    if (currentEvent > previousEvent)
                    {
                        channelsToAnalyze = selectedChannels; //reset channel selection

                        currentData.EventId = currentEvent;
                        currentData.Time = BitConverter.ToUInt32(eventChunks[1].ToArray(), 0);
                        currentData.Sort = BitConverter.ToInt32(eventChunks[2].ToArray(), 0);

                        currentData.Laser1ForwardScatterWidth = BitConverter.ToSingle(eventChunks[3].ToArray(), 0);

                        currentData.Laser1ForwardScatterHeight = Utilities.Clamp(BitConverter.ToSingle(
                            eventChunks[4].ToArray(), 0), _dataMinimum, _dataMaximum);

                        currentData.Laser2ForwardScatterHeight = BitConverter.ToSingle(eventChunks[5].ToArray(), 0);

                        currentData.SideScatterHeight = Utilities.Clamp(BitConverter.ToSingle(
                            eventChunks[6].ToArray(), 0), _dataMinimum, _dataMaximum);

                        currentData.Row = CNM_Def.STRROW_Alphabet[plateRow];
                        currentData.Column = (plateColumn + 1).ToString();

                        if (numExtraParameters > 0)
                        {
                            int chunkIndex = 7;

                            if (CheckFilter(ChannelFilter.FL11, ref channelsToAnalyze))
                            {
                                currentData.Laser1PMT1 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }

                            if (CheckFilter(ChannelFilter.FL12, ref channelsToAnalyze))
                            {
                                currentData.Laser1PMT2 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }

                            if (CheckFilter(ChannelFilter.FL13, ref channelsToAnalyze))
                            {
                                currentData.Laser1PMT3 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }

                            if (CheckFilter(ChannelFilter.FL14, ref channelsToAnalyze))
                            {
                                currentData.Laser1PMT4 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }

                            if (CheckFilter(ChannelFilter.FL15, ref channelsToAnalyze))
                            {
                                currentData.Laser1PMT5 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }

                            if (CheckFilter(ChannelFilter.FL16, ref channelsToAnalyze))
                            {
                                currentData.Laser1PMT6 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }

                            if (CheckFilter(ChannelFilter.FL21, ref channelsToAnalyze))
                            {
                                currentData.Laser2PMT1 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }

                            if (CheckFilter(ChannelFilter.FL22, ref channelsToAnalyze))
                            {
                                currentData.Laser2PMT2 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }

                            if (CheckFilter(ChannelFilter.FL23, ref channelsToAnalyze))
                            {
                                currentData.Laser2PMT3 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }

                            if (CheckFilter(ChannelFilter.FL24, ref channelsToAnalyze))
                            {
                                currentData.Laser2PMT4 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }

                            if (CheckFilter(ChannelFilter.FL25, ref channelsToAnalyze))
                            {
                                currentData.Laser2PMT5 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }

                            if (CheckFilter(ChannelFilter.FL26, ref channelsToAnalyze))
                            {
                                currentData.Laser2PMT6 = Utilities.Clamp(BitConverter.ToSingle(
                                    eventChunks[chunkIndex].ToArray(), 0), _dataMinimum, _dataMaximum);
                                chunkIndex++;
                            }
                        }

                        if (currentEvent > 0) // Only add valid events (A5s convert to large negative number)
                        {
                            eventList.Add(currentData);
                        }

                        if (_logData) { _logBuffer.Add($"{currentData.EventId},{currentData.Time},{subPacket}"); }

                        if (_printConsoleOutput)
                        {
                            _consoleBuffer.Add(currentEvent.ToString());
                            _consoleBuffer.Add(subPacket);
                        }

                        if (packetIndex > 450)
                            searchPacket = false;

                        previousEvent = currentEvent;
                    }
                    else //no more data in the packet.
                    {
                        searchPacket = false;
                        _lastEvent = previousEvent;
                        _previousPacket = new List<byte>(packet);
                        SendConsoleData();
                    }
                }
            }
            return eventList;
        }

        private bool CheckFilter(ChannelFilter channelFilter, ref int selectedChannels)
        {
            bool dataFound = (selectedChannels >= (int)channelFilter);
            if (dataFound) { selectedChannels -= (int)channelFilter; }
            return dataFound;
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