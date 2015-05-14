using System.IO;
using Windows.Networking.Sockets;

namespace Tests.Phone
{
    static class StreamSocketHelper
    {
        public static void SendMessage(this StreamSocket connectedSocket, string message)
        {
            var writer = new BinaryWriter(connectedSocket.OutputStream.AsStreamForWrite());
            writer.Write(message);
            writer.Flush();
        }

        public static string ReciveMessage(this StreamSocket connectedSocket)
        {
            //string message = string.Empty;
            //var thread = new Task(() =>
            //{
            //    var reader = new BinaryReader(connectedSocket.InputStream.AsStreamForRead());
            //    message = reader.ReadString();
            //});

            //thread.Start();
            //var success = thread.Wait(TimeSpan.FromSeconds(2));
            //if (!success)
            //    throw new TimeoutException();

            return new BinaryReader(connectedSocket.InputStream.AsStreamForRead()).ReadString();
        }
    }
}