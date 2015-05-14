using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using TransmissionTests;

namespace Tests.Phone
{
    [TestClass]
    public class TcpClientTransmissionProtocolTests
    {
        private const string Ip = "127.0.0.1";
        private const string Port = "9999";
        private const string TestMessage = "Test Message";

        private ConcurrentBag<StreamSocket> _connectedSockets;
        private TcpClientTransmissionProtocol _protocol;
        private StreamSocketListener _testSocket;

        [TestInitialize]
        public async Task Setup()
        {
            _connectedSockets = new ConcurrentBag<StreamSocket>(); 
            _testSocket = new StreamSocketListener();
            _testSocket.ConnectionReceived += (sender, args) =>
            {
                _connectedSockets.Add(args.Socket);
            };
            await _testSocket.BindEndpointAsync(new HostName(Ip), Port);
        }

        private async Task CreateConnectedProtocolFromTcpClientAsync()
        {
            var client = new StreamSocket();
            await client.ConnectAsync(new HostName(Ip), Port);
            _protocol = new TcpClientTransmissionProtocol(client);
        }

        [TestCleanup]
        public async Task Teardown()
        {
            try
            {
                await _protocol.StopAsync();
                _testSocket.Dispose();
            }
            catch
            {
            }
        }

        [TestMethod]
        public async Task StartConnectsToSocket()
        {
            _protocol = new TcpClientTransmissionProtocol(Ip, Port);
            await _protocol.StartAsync();

            Assert.AreEqual(_connectedSockets.Count, 1);
        }

        [TestMethod]
        public async Task StopCloseConnection()
        {
            var expectedErrors = new[] { SocketErrorStatus.ConnectionResetByPeer, SocketErrorStatus.SoftwareCausedConnectionAbort };
            await CreateConnectedProtocolFromTcpClientAsync();

            StreamSocket connectedSocket;
            Assert.IsTrue(_connectedSockets.TryPeek(out connectedSocket));

            await _protocol.StopAsync();

            try
            {
                connectedSocket.SendMessage("A");
                connectedSocket.SendMessage("A");
                Assert.Fail();
            }
            catch (Exception e)
            {
                CollectionAssert.Contains(expectedErrors, SocketError.GetStatus(e.InnerException.HResult));               
            }            
        }

        [TestMethod]
        public async Task PacketIsCorrectlyRecived()
        {
            await CreateConnectedProtocolFromTcpClientAsync();

            StreamSocket connectedSocket;
            Assert.IsTrue(_connectedSockets.TryPeek(out connectedSocket));
            connectedSocket.SendMessage(TestMessage);

            await Task.Delay(1000);

            CollectionAssert.AreEqual(_protocol.GetPackets().ToArray(), new [] { TestMessage });
        }

        [TestMethod]
        public async Task PacketIsCorrectlySend()
        {
            await CreateConnectedProtocolFromTcpClientAsync();
            StreamSocket connectedSocket;
            Assert.IsTrue(_connectedSockets.TryPeek(out connectedSocket));

            _protocol.SendPacket(TestMessage);

            await Task.Delay(50);

            Assert.AreEqual(connectedSocket.ReciveMessage(), TestMessage);
        }

        [TestMethod]
        public async Task MultiplePacketsAreCorrectlyRecived()
        {
            await CreateConnectedProtocolFromTcpClientAsync();

            StreamSocket connectedSocket;
            Assert.IsTrue(_connectedSockets.TryPeek(out connectedSocket));

            connectedSocket.SendMessage("Test Message 1");
            connectedSocket.SendMessage("Test Message 2");
            connectedSocket.SendMessage("Test Message 3");

            await Task.Delay(1000);

            CollectionAssert.AreEqual(_protocol.GetPackets().ToArray(),
                new[]{"Test Message 1", "Test Message 2", "Test Message 3"});
        }

        [TestMethod]
        public async Task MultiplePacketsAreCorrectlySend()
        {
            var testMessages = new[] {"Test Message 1", "Test Message 2", "Test Message 3"};
            await CreateConnectedProtocolFromTcpClientAsync();
            StreamSocket connectedSocket;
            Assert.IsTrue(_connectedSockets.TryPeek(out connectedSocket));

            foreach (var message in testMessages)
            {
                _protocol.SendPacket(message);
            }

            await Task.Delay(25);

            var recivedMessages = new string[3];
            for (int i = 0; i < 3; i++)
            {
                recivedMessages[i] = connectedSocket.ReciveMessage();
            }

            CollectionAssert.AreEqual(recivedMessages, testMessages);
        }

        [TestMethod]
        public async Task BigPacketIsCorrectlyRecived()
        {
            await CreateConnectedProtocolFromTcpClientAsync();

            var bigString = Enumerable.Range(0, 512)
                .Select(q => "a")
                .Aggregate((source, text) => source + text);

            StreamSocket connectedSocket;
            Assert.IsTrue(_connectedSockets.TryPeek(out connectedSocket));
            connectedSocket.SendMessage(bigString);

            await Task.Delay(2000);

            CollectionAssert.AreEqual(_protocol.GetPackets().ToArray(), new[] { bigString });
        }

        [TestMethod]
        public async Task SendAndReciveWorksTogether()
        {
            const string clientTestMessage = TestMessage + "C";
            const string serverTestMessage = TestMessage + "S";
            await CreateConnectedProtocolFromTcpClientAsync();
            StreamSocket connectedSocket;
            Assert.IsTrue(_connectedSockets.TryPeek(out connectedSocket));

            _protocol.SendPacket(clientTestMessage);
            connectedSocket.SendMessage(serverTestMessage);

            await Task.Delay(50);

            CollectionAssert.AreEqual(_protocol.GetPackets().ToArray(), new[]{serverTestMessage});
            Assert.AreEqual(connectedSocket.ReciveMessage(), clientTestMessage);
        }

        [TestMethod]
        public async Task ThreadsAreCorrectlyDisposed()
        {
            await CreateConnectedProtocolFromTcpClientAsync();
            StreamSocket connectedSocket;
            Assert.IsTrue(_connectedSockets.TryPeek(out connectedSocket));

            await _protocol.StopAsync();
            connectedSocket.Dispose();

            await Task.Delay(5000);

            Assert.AreNotEqual(_protocol.RecieveThread.Status, TaskStatus.Running);
            Assert.AreNotEqual(_protocol.SendThread.Status, TaskStatus.Running);
        }
    }
}
