using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TransmissionTests
{
    public abstract class AbstractTransmissionProtocol
    {
        protected abstract Task OpenConnectionAsync();
        protected abstract Task CloseConnectionAsync();
        protected abstract bool BeenOpened { get; }
        protected abstract bool BeenClosed { get; }
        protected abstract ConcurrentQueue<string> RecivedPacketsQueue { get; }
        protected abstract ConcurrentQueue<string> PacketsToSendQueue { get; } 

        public IEnumerable<string> GetPackets()
        {
            ValidateSocket();

            var returnList = new List<string>();
            var count = RecivedPacketsQueue.Count;
            for (int i = 0; i < count; i++)
            {
                string message;
                if (RecivedPacketsQueue.TryDequeue(out message))
                {
                    returnList.Add(message);
                }
            }
            return returnList;
        }

        public void SendPacket(string packet)
        {
            ValidateSocket();

            PacketsToSendQueue.Enqueue(packet);
        }

        public async Task StartAsync()
        {
            if (BeenOpened)
                throw new InvalidOperationException("Connection has already started");

            await OpenConnectionAsync();
        }

        public async Task StopAsync()
        {
            if (BeenClosed)
                throw new InvalidOperationException("Connection has already stopped");

            await CloseConnectionAsync();
        }

        private void ValidateSocket()
        {
            if (BeenClosed)
                throw new InvalidOperationException("Connection has been closed.");
            if (!BeenOpened)
                throw new InvalidOperationException("Connection has not been opened.");
        }
    }
}