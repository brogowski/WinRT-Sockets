using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace TransmissionTests
{
    public class TcpClientTransmissionProtocol : AbstractTransmissionProtocol
    {
        private readonly string _ip;
        private readonly string _port;
        private readonly ConcurrentQueue<string> _synchronizedReciveQueue;
        private readonly ConcurrentQueue<string> _synchronizedSendQueue;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private StreamSocket _tcpClient;
        private bool _beenClosed;
        private bool _beenOpened;

        internal Task RecieveThread { get; private set; }
        internal Task SendThread { get; private set; }

        public TcpClientTransmissionProtocol(string ip, string port)
        {
            _ip = ip;
            _port = port;
            _synchronizedReciveQueue = new ConcurrentQueue<string>();
            _synchronizedSendQueue = new ConcurrentQueue<string>();
        }

        public TcpClientTransmissionProtocol(StreamSocket client)
        {
            _tcpClient = client;
            _ip = client.Information.RemoteAddress.CanonicalName;
            _port = client.Information.RemotePort;

            _synchronizedReciveQueue = new ConcurrentQueue<string>();
            _synchronizedSendQueue = new ConcurrentQueue<string>();

            SetupThreads();

            _beenOpened = true;
        }

        protected override async Task OpenConnectionAsync()
        {
            _tcpClient = new StreamSocket
            {
                Control = { KeepAlive = true, NoDelay = true, QualityOfService = SocketQualityOfService.LowLatency}
            };
            await _tcpClient.ConnectAsync(new HostName(_ip), _port);
            _beenOpened = true;            
            
            SetupThreads();
        }

        protected override async Task CloseConnectionAsync()
        {
            _cancellationTokenSource.Cancel();
            _tcpClient.Dispose();
            _beenClosed = true;                                
        }

        protected override bool BeenOpened
        {
            get { return _beenOpened; }
        }
        protected override bool BeenClosed
        {
            get { return _beenClosed; }
        }

        protected override ConcurrentQueue<string> RecivedPacketsQueue
        {
            get { return _synchronizedReciveQueue; }
        }
        protected override ConcurrentQueue<string> PacketsToSendQueue
        {
            get { return _synchronizedSendQueue; }
        }

        private void ReciveMessages(IInputStream networkStream, CancellationToken token)
        {
            var reader = new BinaryReader(networkStream.AsStreamForRead());
            try
            {
                Task.Run(() => KeepReceivingMessages(reader, token)).Wait(token);
            }
            finally
            {           
            }
        }
        private void SendMessages(IOutputStream networkStream, CancellationToken token)
        {
            var writer = new BinaryWriter(networkStream.AsStreamForWrite());
            try
            {
                KeepSendingMessages(writer, token);
            }
            finally
            {
            }
        }

        private void KeepReceivingMessages(BinaryReader reader, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var message = reader.ReadString();
                _synchronizedReciveQueue.Enqueue(message);
            }
        }

        private void KeepSendingMessages(BinaryWriter writer, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_synchronizedSendQueue.IsEmpty)
                {
                    Task.Delay(10).Wait();
                    continue;
                }

                string message;
                if (_synchronizedSendQueue.TryDequeue(out message))
                {
                    writer.Write(message);
                    writer.Flush();
                }
            }
        }

        private void SetupThreads()
        {
            var inputStream = _tcpClient.InputStream;
            var outputStream = _tcpClient.OutputStream;
            var cancelToken = _cancellationTokenSource.Token;

            RecieveThread = new Task(() => ReciveMessages(inputStream, cancelToken));
            SendThread = new Task(() => SendMessages(outputStream, cancelToken));
            RecieveThread.Start();
            SendThread.Start();
        }
    }
}