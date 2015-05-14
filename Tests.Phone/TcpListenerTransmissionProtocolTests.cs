using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using TransmissionTests;

namespace Tests.Phone
{
    [TestClass]
    public class TcpListenerTransmissionProtocolTests
    {
        private const string Ip = "127.0.0.1";
        private const string Port = "9999";

        private TcpListenerTransmissionProtocol _protocol;
        private StreamSocket _testSocket;
        private const string TestMessage = "Test Message";

        [TestInitialize]
        public void Setup()
        {
            _testSocket = new StreamSocket();
            _testSocket.Control.QualityOfService = SocketQualityOfService.LowLatency;
            _testSocket.Control.NoDelay = true;
            _testSocket.Control.KeepAlive = true;
            _protocol = new TcpListenerTransmissionProtocol(Ip, Port);
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
        public async Task StartAllowsConnections()
        {           
            await _protocol.StartAsync();
            await ConnectTestSocket();                        
        }

        [TestMethod]
        public async Task StopClosesConnections()
        {
            var expectedErrors = new[] { SocketErrorStatus.ConnectionResetByPeer, SocketErrorStatus.SoftwareCausedConnectionAbort };

            await _protocol.StartAsync();
            await ConnectTestSocket();
            await _protocol.StopAsync();

            await Task.Delay(1000);

            try
            {
                _testSocket.SendMessage("AAAAAAAAAAA");
                _testSocket.SendMessage("AAAAA");
                _testSocket.SendMessage("AAAAAAA");
                _testSocket.SendMessage("AAAAAAA");                
                Assert.Fail();
            }
            catch (Exception e)
            {
                CollectionAssert.Contains(expectedErrors, SocketError.GetStatus(e.InnerException.HResult));
            }
        }

        [TestMethod]
        public async Task PacketIsCorrectlySend()
        {
            await _protocol.StartAsync();
            await ConnectTestSocket();

            _protocol.SendPacket(TestMessage);

            Assert.AreEqual(_testSocket.ReciveMessage(), TestMessage);
        }

        [TestMethod]
        public async Task PacketIsCorrectlyRecieved()
        {
            await _protocol.StartAsync();
            await ConnectTestSocket();

            _testSocket.SendMessage(TestMessage);

            await Task.Delay(100);

            CollectionAssert.AreEqual(_protocol.GetPackets().ToArray(), new[]{TestMessage});
        }

        [TestMethod]
        public async Task MultiplePacketsAreCorrectlyRecived()
        {
            await _protocol.StartAsync();
            await ConnectTestSocket();

            _testSocket.SendMessage("Test Message 1");
            _testSocket.SendMessage("Test Message 2");
            _testSocket.SendMessage("Test Message 3");

            await Task.Delay(100);

            CollectionAssert.AreEqual(_protocol.GetPackets().ToArray(),
                new[]{"Test Message 1", "Test Message 2", "Test Message 3"});
        }

        [TestMethod]
        public async Task MultiplePacketsAreCorrectlySend()
        {
            var testMessages = new[] { "Test Message 1", "Test Message 2", "Test Message 3" };
            await _protocol.StartAsync();
            await ConnectTestSocket();

            foreach (var message in testMessages)
            {
                _protocol.SendPacket(message);
            }

            await Task.Delay(25);

            var recivedMessages = new string[3];
            for (int i = 0; i < 3; i++)
            {
                recivedMessages[i] = _testSocket.ReciveMessage();
            }

            CollectionAssert.AreEqual(recivedMessages, testMessages);
        }

        [TestMethod]
        public async Task BigPacketIsCorrectlyRecived()
        {
            await _protocol.StartAsync();
            await ConnectTestSocket();

            var bigString = Enumerable.Range(0, 512)
                .Select(q => "a")
                .Aggregate((source, text) => source + text);

            _testSocket.SendMessage(bigString);

            await Task.Delay(100);

            CollectionAssert.AreEqual(_protocol.GetPackets().ToArray(),
                new[]{bigString});
        }

        [TestMethod]
        public async Task SendAndReciveWorksTogether()
        {
            const string clientTestMessage = TestMessage + "C";
            const string serverTestMessage = TestMessage + "S";
            await _protocol.StartAsync();
            await ConnectTestSocket();
            
            _protocol.SendPacket(clientTestMessage);
            _testSocket.SendMessage(serverTestMessage);

            await Task.Delay(25);

            CollectionAssert.AreEqual(_protocol.GetPackets().ToArray(), new[]{serverTestMessage});
            Assert.AreEqual(_testSocket.ReciveMessage(), clientTestMessage);
        }

        [TestMethod]
        public async Task MultipleClientsAreSupported()
        {
            var clients = new[]
            {
                GetNewStreamSocket(),
                GetNewStreamSocket(),
                GetNewStreamSocket()
            };

            await _protocol.StartAsync();

            foreach (var client in clients)
            {
                await client.ConnectAsync(new HostName(Ip), Port);
            }

            for (int index = 0; index < clients.Length; index++)
            {
                clients[index].SendMessage("Client: " + index);
            }           
 
            _protocol.SendPacket("Server");

            await Task.Delay(5000);

            foreach (var client in clients)
            {
                Assert.AreEqual(client.ReciveMessage(), "Server");
            }

            CollectionAssert.AreEqual(_protocol.GetPackets().ToArray(),
                Enumerable.Range(0, clients.Length)
                .Select(i => "Client: " + i).ToArray());
        }

        [TestMethod]
        public async Task ThreadsAreCorrectlyDisposed()
        {
            await _protocol.StartAsync();
            await ConnectTestSocket();

            await _protocol.StopAsync();
            _testSocket.Dispose();

            await Task.Delay(25);

            Assert.AreNotEqual(_protocol.RecieveThread.Status, TaskStatus.Running);
            Assert.AreNotEqual(_protocol.SendThread.Status, TaskStatus.Running);
        }

        private async Task ConnectTestSocket()
        {
            await _testSocket.ConnectAsync(new HostName(Ip), Port);
        }

        private StreamSocket GetNewStreamSocket()
        {
            var socket = new StreamSocket();
            socket.Control.KeepAlive = true;
            socket.Control.NoDelay = true;
            socket.Control.QualityOfService = SocketQualityOfService.LowLatency;
            return socket;
        }

    }
}
