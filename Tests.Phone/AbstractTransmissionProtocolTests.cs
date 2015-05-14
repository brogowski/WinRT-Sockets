using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using TransmissionTests;

namespace Tests.Phone
{
    [TestClass]
    public class AbstractTransmissionProtocolTests
    {
        private MockTransmissionProtocol _transmissionProtocol;

        [TestInitialize]
        public void Setup()
        {
            _transmissionProtocol = new MockTransmissionProtocol();
        }

        [TestMethod]
        public async Task StartOpensConnection()
        {
            await _transmissionProtocol.StartAsync();

            Assert.IsTrue(_transmissionProtocol.OpenConnectionInvoked);
        }

        [TestMethod]
        public async Task StopClosesConnection()
        {
            await _transmissionProtocol.StopAsync();

            Assert.IsTrue(_transmissionProtocol.CloseConnectionInvoked);
        }

        [TestMethod]
        public void WhenConnectionIsNotOpenGetPacketsThrowsException()
        {
            try
            {
                _transmissionProtocol.GetPackets();
                Assert.Fail();
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual(e.Message, "Connection has not been opened.");
            }
        }

        [TestMethod]
        public void WhenConnectionIsClosedGetPacketsThrowsException()
        {
            _transmissionProtocol.BeenCloseValue = true;

            try
            {
                _transmissionProtocol.GetPackets();
                Assert.Fail();
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual(e.Message, "Connection has been closed.");
            }
        }

        [TestMethod]
        public void GetPacketsReturnsNotNull()
        {
            _transmissionProtocol.BeenOpenedValue = true;

            Assert.IsNotNull(_transmissionProtocol.GetPackets());
        }

        [TestMethod]
        public void GetPacketsReturnsCorrectPackets()
        {
            _transmissionProtocol.BeenOpenedValue = true;
            _transmissionProtocol.RecivedPackets.Enqueue("A");
            _transmissionProtocol.RecivedPackets.Enqueue("B");
            _transmissionProtocol.RecivedPackets.Enqueue("C");

            CollectionAssert.AreEqual(_transmissionProtocol.GetPackets().ToArray(), new[] {"A", "B", "C"});
        }

        [TestMethod]
        public void SendPacketAddPacketToQueue()
        {
            _transmissionProtocol.BeenOpenedValue = true;

            _transmissionProtocol.SendPacket("A");

            CollectionAssert.AreEqual(_transmissionProtocol.PacketsToSend, new[] { "A" });
        }

        [TestMethod]
        public async Task StartSecondTimeThrowsException()
        {
            _transmissionProtocol.BeenOpenedValue = true;

            try
            {
                await _transmissionProtocol.StartAsync();
                Assert.Fail();
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual(e.Message, "Connection has already started");
            }
        }

        [TestMethod]
        public async Task StopSecondTimeThrowsException()
        {
            _transmissionProtocol.BeenCloseValue = true;

            try
            {
                await _transmissionProtocol.StopAsync();
                Assert.Fail();
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual(e.Message, "Connection has already stopped");
            }
        }
    }

    public class MockTransmissionProtocol : AbstractTransmissionProtocol
    {
        protected override async Task OpenConnectionAsync()
        {
            OpenConnectionInvoked = true;
        }
        
        protected override async Task CloseConnectionAsync()
        {
            CloseConnectionInvoked = true;
        }

        protected override bool BeenOpened
        {
            get { return BeenOpenedValue; }
        }

        protected override bool BeenClosed
        {
            get { return BeenCloseValue; }
        }

        protected override ConcurrentQueue<string> RecivedPacketsQueue
        {
            get { return RecivedPackets; }
        }

        protected override ConcurrentQueue<string> PacketsToSendQueue
        {
            get { return PacketsToSend; }
        }

        public bool OpenConnectionInvoked;
        public bool CloseConnectionInvoked;
        public bool BeenOpenedValue;
        public bool BeenCloseValue;
        public ConcurrentQueue<string> RecivedPackets = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> PacketsToSend = new ConcurrentQueue<string>();
    }
}
