using System;
using System.Collections.Concurrent;

namespace PalaLite.Models
{
    internal class PacketManager
    {
        //Cell SortingData
        int bulkSortingCount;

        //private int _packetLength = 512;
        private ConcurrentQueue<byte[]> _packets;

        private int _packetsToAnalyze = 100;
        private int _packetCount;
        private bool _done;

        public event EventHandler DoneEventHandler;
        public event EventHandler<EventArgs> PacketAvailableEventHandler;

        public PacketManager() 
        {
            _packets = new ConcurrentQueue<byte[]>();
        }

        public void Add(byte[] data)
        {
            if (_packetCount < _packetsToAnalyze)
            {
                _packets.Enqueue(data);
                EventArgs args = new EventArgs();
                OnPacketAvailable(args);
                _packetCount++;
            } else
            {
                if (!_done) { Done(); }
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
