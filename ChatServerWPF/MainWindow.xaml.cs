using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace _03_ChatServerWPF
{
    public partial class MainWindow
    {
        private TcpListener tcpListener;
        private NetworkStream _networkStream;
        private bool _serverStarted;
        private List<TcpClient> _clientList = new List<TcpClient>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void AddMessage(string message)
        {
            Dispatcher.Invoke(() => listChats.Items.Add(message));
        }

        private async void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if ((string) btnStartStop.Content == "Start")
            {
                btnStartStop.Content = "Stop";
                var serverPort = ParseStringToInt(serverPortValue.Text);
                var serverBufferSize = ParseStringToInt(serverBufferSizeValue.Text);
                await Listener(serverPort, serverBufferSize);
            }
            else
            {
                btnStartStop.Content = "Start";
                StopServer();
            }
        }

        private async Task Listener(int serverPort, int serverBufferSize, bool stopServer = false)
        {
            if (!stopServer)
            {
                _serverStarted = true;
                tcpListener = new TcpListener(IPAddress.Any, serverPort);
                tcpListener.Start();
                AddMessage($"[SERVER]: Started on port {serverPort.ToString()}");
            }
            else _serverStarted = false;

            while (_serverStarted)
            {
                try
                {
                    Debug.WriteLine("Test #1");
                    var tcpClient = await tcpListener.AcceptTcpClientAsync();
                    AddMessage("[SERVER]: Client connected!");
                    _clientList.Add(tcpClient);
                    Debug.WriteLine("Test #2");
                    await Task.Run(() => ReceiveData(tcpClient, serverBufferSize));
                    Debug.WriteLine("Test #3");
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }

            Debug.WriteLine("SERVER IS STOPPED!!!");
            tcpListener.Stop();
        }

        private async void ReceiveData(TcpClient tcpClient, int bufferSize)
        {
            var buffer = new byte[bufferSize];
            var networkStream = tcpClient.GetStream();

            const string connectSignal = "CONNECT~";
            const string messageSignal = "MESSAGE~";
            const string serverDisconnectSignal = "DISCONNECT_SERVER~";
            const string clientDisconnectSignal = "DISCONNECTED_CLIENT~";

            while (networkStream.CanRead)
            {
                var incomingData = "";
                string message = "";

                try
                {
                    while (incomingData.IndexOf("~") < 0)
                    {
                        Debug.WriteLine("✅ Incoming message");
                        var bytes = await networkStream.ReadAsync(buffer, 0, bufferSize);
                        message = Encoding.ASCII.GetString(buffer, 0, bytes);
                        incomingData += message;
                    }
                }
                catch (IOException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    networkStream.Close();
                    break;
                }

                Debug.WriteLine($"Message: {incomingData}");

                /***
                 * New client connected
                 */
                if (incomingData.EndsWith(connectSignal))
                {
                    var clientName = incomingData.Remove(incomingData.Length - connectSignal.Length);
                    Dispatcher.Invoke((() => listClients.Items.Add(clientName)));
                    await SendMessageToClients($"[SERVER]: {clientName} connected!~");
                }

                /***
                 * New chat message
                 */
                if (incomingData.EndsWith(messageSignal))
                {
                    var chatMessage = incomingData.Remove(incomingData.Length - messageSignal.Length);
                    AddMessage(chatMessage);
                    await SendMessageToClients($"{chatMessage}~");
                }

                /***
                 * Client disconnected
                 */
                if (incomingData.EndsWith(clientDisconnectSignal))
                {
                    message = incomingData.Remove(incomingData.Length - clientDisconnectSignal.Length);
                    AddMessage(message);
                    await SendMessageToClients($"{message}~");
                    await SendDisconnectMessageToClient(tcpClient, clientDisconnectSignal);
                    Dispatcher.Invoke(() => listClients.Items.RemoveAt(_clientList.IndexOf(tcpClient)));
                    _clientList.Remove(tcpClient);
                }
            }
        }

        private async void StopServer()
        {
            const string disconnectingMessage = "[SERVER]: Server is closed!DISCONNECTED_SERVER~";
            await Task.Run(() => SendMessageToClients(disconnectingMessage));
            
            foreach (var client in _clientList)
            {
                Debug.WriteLine("Client X stopped!");
                client.Close();
            }

            _clientList = new List<TcpClient>();
            Dispatcher.Invoke(() => listClients.Items.Clear());

            _serverStarted = false;

            AddMessage("[SERVER]: Stopped");

            var serverPort = ParseStringToInt(serverPortValue.Text);
            var serverBufferSize = ParseStringToInt(serverBufferSizeValue.Text);
            await Listener(serverPort, serverBufferSize, true);
        }

        private async Task SendMessageToClients(string message)
        {
            if (_clientList.Count > 0)
            {
                foreach (var client in _clientList)
                {
                    _networkStream = client.GetStream();
                    if (!_networkStream.CanRead) continue;
                    var serverMessageByteArray = Encoding.ASCII.GetBytes(message);
                    await _networkStream.WriteAsync(serverMessageByteArray, 0, serverMessageByteArray.Length);
                }
            }
        }

        private async Task SendDisconnectMessageToClient(TcpClient tcpClient, string message)
        {
            _networkStream = tcpClient.GetStream();
            if (_networkStream.CanRead)
            {
                var serverMessageByteArray = Encoding.ASCII.GetBytes(message);
                await _networkStream.WriteAsync(serverMessageByteArray, 0, serverMessageByteArray.Length);
            }
        }

        private int ParseStringToInt(string stringVal)
        {
            int.TryParse(stringVal, out var intVal);
            return intVal;
        }
    }
}