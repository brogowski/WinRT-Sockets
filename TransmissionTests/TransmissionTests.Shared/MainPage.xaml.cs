using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Connectivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace TransmissionTests
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private TcpListenerTransmissionProtocol _host;
        private TcpClientTransmissionProtocol _client;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void ConnectAsHost(object sender, RoutedEventArgs e)
        {
            if (_host == null)
            {
                var text = IpTextBox.Text;
                _host = new TcpListenerTransmissionProtocol(text.Split(':')[0], text.Split(':')[1]);
                await _host.StartAsync();
            }
        }

        private async void ConnectAsClient(object sender, RoutedEventArgs e)
        {
            if (_client == null)
            {
                var text = IpTextBox.Text;
                _client = new TcpClientTransmissionProtocol(text.Split(':')[0], text.Split(':')[1]);
                await _client.StartAsync();
            }
        }

        private void SendMessage(object sender, RoutedEventArgs e)
        {
            if (_client != null)
            {
                _client.SendPacket(SendMessageTextBox.Text);
                SendMessageTextBox.Text = "";
            }
            if (_host != null)
            {
                _host.SendPacket(SendMessageTextBox.Text);
                SendMessageTextBox.Text = "";
            }
        }

        private void ReciveMessage(object sender, RoutedEventArgs e)
        {
            if (_client != null)
            {
                var packets = _client.GetPackets();
                if(packets.Any())
                    GetMessageTextBox.Text += Environment.NewLine + packets.Aggregate((s, s1) => string.Join(Environment.NewLine, s, s1));
            }
            if (_host != null)
            {
                var packets = _host.GetPackets();
                if (packets.Any())
                    GetMessageTextBox.Text += Environment.NewLine + packets.Aggregate((s, s1) => string.Join(Environment.NewLine, s, s1));
            }
        }

        private async void Disconnect(object sender, RoutedEventArgs e)
        {
            if (_client != null)
            {
                await _client.StopAsync();
                _client = null;
            }
            if (_host != null)
            {
                await _host.StopAsync();
                _host = null;
            }
        }
    }
}
