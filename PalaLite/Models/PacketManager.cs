using System;
using System.Collections.Concurrent;
using ShellProgressBar;

namespace PalaLite.Models
{
    internal class PacketManager
    {
        //Cell SortingData
        int bulkSortingCount;

        //private int _packetLength = 512;
        private ConcurrentQueue<byte[]> _packets;

        private int _packetsToAnalyze = 1000;
        public int PacketsToAnalyze { get => _packetsToAnalyze; set => _packetsToAnalyze = value; }
        private int _packetCount;
        private bool _done;

        public event EventHandler DoneEventHandler;
        public event EventHandler<EventArgs> PacketAvailableEventHandler;

        private ProgressBar _pbar;
        private ProgressBarOptions _pbarOptions;
        private bool _enablePbar = true;

        public PacketManager() 
        {
            _packets = new ConcurrentQueue<byte[]>();
            if (_enablePbar)
            {
                _pbarOptions = new ProgressBarOptions
                {
                    ProgressCharacter = '-',
                    ProgressBarOnBottom = true,
                };
            }
        }

        public void Add(byte[] data)
        {
            if (_packetCount < _packetsToAnalyze)
            {
                _packets.Enqueue((byte[])data.Clone());
                //EventArgs args = new EventArgs();
                //OnPacketAvailable(args);
                _packetCount++;
            } else
            {
                if (!_done) { Done(); }
            }
            if (_enablePbar) 
            {
                if (_pbar == null)
                {
                    _pbar = new ProgressBar(PacketsToAnalyze, $"Packets Received (First Event: {CellPMTDataDecoder.EventNumber(data)})", _pbarOptions);
                }
                _pbar.Tick($"Packets Received (First Event: {CellPMTDataDecoder.EventNumber(data)})");
            }
        }

        public (bool, byte[]) GetNextPacket()
        {
            byte[] packet;
            bool success = _packets.TryDequeue(out packet);
            return (success, packet);
        }

        public bool DataAvailable() => !_packets.IsEmpty;

        protected virtual void OnPacketAvailable(EventArgs e)
        {
            EventHandler<EventArgs> handler = PacketAvailableEventHandler;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void Done()
        {
            _done = true;
            if (_enablePbar) { 
                _pbar.WriteLine("Complete!");
                _pbar.Dispose();
            }
            Console.WriteLine($"Packets received, analyzing...{Environment.NewLine}");
            OnDone(new EventArgs());
        }

        protected virtual void OnDone(EventArgs e)
        {
            EventHandler handler = DoneEventHandler;
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }

    /*public class PacketAvailableEventArgs : EventArgs
    {
        public byte[] Packet { get; set; }
    }*/
}
