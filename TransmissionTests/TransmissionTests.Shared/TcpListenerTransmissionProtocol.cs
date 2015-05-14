using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;

namespace TransmissionTests
{
    public class TcpListenerTransmissionProtocol : AbstractTransmissionProtocol
    {
        private readonly string _ip;
        private readonly string _port;
        private readonly StreamSocketListener _listener;
        private readonly ConcurrentQueue<string> _synchronizedSendQueue;
        private readonly ConcurrentQueue<string> _synchronizedReciveQueue;
        private readonly ConcurrentBag<TcpClientTransmissionProtocol> _clients;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private bool _beenOpened;
        private bool _beenClosed;

        internal Task SendThread { get; private set; }
        internal Task RecieveThread { get; private set; }

        public TcpListenerTransmissionProtocol(string ip, string port)
        {
            _ip = ip;
            _port = port;
            _listener = new StreamSocketListener();
            _listener.Control.QualityOfService = SocketQualityOfService.LowLatency;             
       
            _synchronizedReciveQueue = new ConcurrentQueue<string>();
            _synchronizedSendQueue = new ConcurrentQueue<string>();
            _clients = new ConcurrentBag<TcpClientTransmissionProtocol>();

        }

        protected override async Task OpenConnectionAsync()
        {
            _listener.ConnectionReceived += AcceptNewSocket;
            await _listener.BindEndpointAsync(new HostName(_ip), _port);
            SetupThreads();
            _beenOpened = true;
        }

        protected override async Task CloseConnectionAsync()
        {
            _cancellationTokenSource.Cancel();
            _listener.Dispose();

            foreach (var tcpClientTransmissionProtocol in _clients)
            {
                await tcpClientTransmissionProtocol.StopAsync();
            }

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

        private void SendMessage()
        {
            try
            {
                KeepSendingMessages(_cancellationTokenSource.Token);
            }
            finally
            {
            }
        }

        private void RecieveMessage()
        {
            try
            {
                KeepRecievingMessages(_cancellationTokenSource.Token);
            }
            finally
            {
            }
        }

        private void KeepSendingMessages(CancellationToken token)
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
                    SendMessageToAllClients(message);
                }
            }
        }

        private void KeepRecievingMessages(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var message in GetMessagesFromClients())
                {
                    _synchronizedReciveQueue.Enqueue(message);
                }
            }
        }

        private void AcceptNewSocket(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            SetupClient(args.Socket);
        }

        private IEnumerable<string> GetMessagesFromClients()
        {
            return _clients.SelectMany(q => q.GetPackets());
        }

        private void SendMessageToAllClients(string message)
        {
            foreach (var client in _clients)
            {
                client.SendPacket(message);
            }
        }

        private void SetupClient(StreamSocket client)
        {
            _clients.Add(new TcpClientTransmissionProtocol(client));
        }

        private void SetupThreads()
        {
            SendThread = new Task(SendMessage);
            RecieveThread = new Task(RecieveMessage);

            SendThread.Start();
            RecieveThread.Start();
        }
    }
}
