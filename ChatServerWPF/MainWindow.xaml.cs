using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace _03_ChatServerWPF
{
    /// <summary>
    /// TCP chat server
    /// </summary>
    public partial class MainWindow
    {
        private TcpListener _tcpListener;
        private NetworkStream _networkStream;
        private List<TcpClient> _clientList = new List<TcpClient>();
        private bool _serverStarted;
        private const string ConnectSignal = "CONNECT~";
        private const string MessageSignal = "MESSAGE~";
        private const string ServerDisconnectSignal = "DISCONNECTED_SERVER~";
        private const string ClientDisconnectSignal = "DISCONNECTED_CLIENT~";

        /// <summary>
        /// Initialize main window
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Add messages to messages ListBox
        /// </summary>
        /// <param name="message"></param>
        private void AddMessage(string message)
        {
            Dispatcher.Invoke(() => listChats.Items.Add(message));
        }

        /// <summary>
        /// Start server and stop server button functionality
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if ((string) btnStartStop.Content == "Start")
            {
                if (IsValidIpAddress(serverIpAddress.Text) && IsValidPort(serverPortValue.Text))
                {
                    btnStartStop.Content = "Stop";
                    var serverPort = ParseStringToInt(serverPortValue.Text);
                    var serverBufferSize = ParseStringToInt(serverBufferSizeValue.Text);
                    IPAddress.TryParse(serverIpAddress.Text, out var ipAddress);
                    await Listener(ipAddress, serverPort, serverBufferSize);
                }
                else
                {
                    MessageBox.Show("IP Address or server port is invalid!");
                }
            }
            else
            {
                btnStartStop.Content = "Start";
                StopServer();
            }
        }

        /// <summary>
        /// This method starts or stops the TCP listener
        /// Also listens for new TCP clients
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="serverPort"></param>
        /// <param name="serverBufferSize"></param>
        /// <param name="stopServer"></param>
        /// <returns></returns>
        private async Task Listener(IPAddress ipAddress, int serverPort, int serverBufferSize, bool stopServer = false)
        {
            try
            {
                if (!stopServer)
                {
                    _serverStarted = true;
                    _tcpListener = new TcpListener(ipAddress, serverPort);
                    _tcpListener.Start();
                    AddMessage($"[SERVER]: Started on port {serverPort.ToString()}");
                }
                else _serverStarted = false;

                while (_serverStarted)
                {
                    try
                    {
                        var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                        AddMessage("[SERVER]: Client connected!");
                        _clientList.Add(tcpClient);
                        await Task.Run(() => ReceiveData(tcpClient, serverBufferSize));
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }

                _tcpListener.Stop();
            }
            catch (SocketException)
            {
                btnStartStop.Content = "Start";
                MessageBox.Show("Server port already in use or the IP Address or server port is invalid!");
            }
        }

        /// <summary>
        /// This method reads all incoming data / messages
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="bufferSize"></param>
        private async void ReceiveData(TcpClient tcpClient, int bufferSize)
        {
            var buffer = new byte[bufferSize];
            var networkStream = tcpClient.GetStream();

            while (networkStream.CanRead)
            {
                var incomingData = "";
                string message = "";

                try
                {
                    while (incomingData.IndexOf("~") < 0)
                    {
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

                /***
                 * New client connected
                 */
                if (incomingData.EndsWith(ConnectSignal))
                {
                    var clientName = incomingData.Remove(incomingData.Length - ConnectSignal.Length);
                    Dispatcher.Invoke((() => listClients.Items.Add(clientName)));
                    await SendMessageToClients($"[SERVER]: {clientName} connected!~");
                }

                /***
                 * New chat message
                 */
                if (incomingData.EndsWith(MessageSignal))
                {
                    var chatMessage = incomingData.Remove(incomingData.Length - MessageSignal.Length);
                    AddMessage(chatMessage);
                    await SendMessageToClients($"{chatMessage}~");
                }

                /***
                 * Client disconnected
                 */
                if (incomingData.EndsWith(ClientDisconnectSignal))
                {
                    message = incomingData.Remove(incomingData.Length - ClientDisconnectSignal.Length);
                    AddMessage(message);
                    await SendMessageToClients($"{message}~");
                    await SendDisconnectMessageToClient(tcpClient, ClientDisconnectSignal);
                    Dispatcher.Invoke(() => listClients.Items.RemoveAt(_clientList.IndexOf(tcpClient)));
                    _clientList.Remove(tcpClient);
                }
            }
        }

        /// <summary>
        /// This method triggers the stop functionality
        /// </summary>
        private async void StopServer()
        {
            var disconnectingMessage = $"[SERVER]: Server is closed!{ServerDisconnectSignal}";
            await Task.Run(() => SendMessageToClients(disconnectingMessage));

            foreach (var client in _clientList) client.Close();

            _clientList = new List<TcpClient>();
            Dispatcher.Invoke(() => listClients.Items.Clear());

            _serverStarted = false;

            AddMessage("[SERVER]: Server is closed!");

            var serverPort = ParseStringToInt(serverPortValue.Text);
            var serverBufferSize = ParseStringToInt(serverBufferSizeValue.Text);
            IPAddress.TryParse(serverIpAddress.Text, out var ipAddress);
            await Listener(ipAddress, serverPort, serverBufferSize, true);
        }

        /// <summary>
        /// This method sends messages to all connected clients
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Sends a disconnect message/signal to the specified client
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task SendDisconnectMessageToClient(TcpClient tcpClient, string message)
        {
            _networkStream = tcpClient.GetStream();
            if (_networkStream.CanRead)
            {
                var serverMessageByteArray = Encoding.ASCII.GetBytes(message);
                await _networkStream.WriteAsync(serverMessageByteArray, 0, serverMessageByteArray.Length);
            }
        }

        /// <summary>
        /// Method is triggered when user closes the application.
        /// This method will stop the server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CloseServerConnection(object sender, CancelEventArgs e)
        {
            if (_tcpListener != null && _tcpListener.Server.Connected)
            {
                var disconnectingMessage = $"[SERVER]: Server is closed!{ServerDisconnectSignal}";
                await Task.Run(() => SendMessageToClients(disconnectingMessage));
                _tcpListener.Stop();
            }
        }

        /// <summary>
        /// Parse string to integer
        /// </summary>
        /// <param name="stringVal"></param>
        /// <returns>
        /// Returns integer
        /// </returns>
        private int ParseStringToInt(string stringVal)
        {
            int.TryParse(stringVal, out var intVal);
            return intVal;
        }

        /// <summary>
        /// Validates IP Address
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns>
        /// True if IP Address is valid
        /// </returns>
        private bool IsValidIpAddress(string ipAddress)
        {
            return IPAddress.TryParse(ipAddress, out var ip);
        }

        /// <summary>
        /// Validates Server port
        /// </summary>
        /// <param name="port"></param>
        /// <returns>
        /// True if server port is valid
        /// </returns>
        private bool IsValidPort(string port)
        {
            const int maxPortNumber = 65535;
            const int minPortNumber = 0;
            return (port.All(char.IsDigit) && (ParseStringToInt(port) > minPortNumber) &&
                    (ParseStringToInt(port) <= maxPortNumber));
        }
    }
}