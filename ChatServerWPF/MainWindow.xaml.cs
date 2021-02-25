using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private TcpListener _tcpListener;
        private NetworkStream _networkStream;
        private List<TcpClient> _clientList = new List<TcpClient>();
        private bool _serverStarted;

        private const string ConnectSignal = "CONNECT~";
        private const string MessageSignal = "MESSAGE~";
        private const string ServerDisconnectSignal = "DISCONNECT_SERVER~";
        private const string ClientDisconnectSignal = "DISCONNECTED_CLIENT~";

        /// <summary>
        /// 
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        private void AddMessage(string message)
        {
            Dispatcher.Invoke(() => listChats.Items.Add(message));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serverPort"></param>
        /// <param name="serverBufferSize"></param>
        /// <param name="stopServer"></param>
        /// <returns></returns>
        private async Task Listener(int serverPort, int serverBufferSize, bool stopServer = false)
        {
            if (!stopServer)
            {
                _serverStarted = true;
                _tcpListener = new TcpListener(IPAddress.Any, serverPort);
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

        /// <summary>
        /// 
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
        /// 
        /// </summary>
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

        /// <summary>
        /// 
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
        /// 
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
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CloseServerConnection(object sender, CancelEventArgs e)
        {
            if (_tcpListener.Server.Connected)
            {
                const string disconnectingMessage = "[SERVER]: Server is closed!DISCONNECTED_SERVER~";
                await Task.Run(() => SendMessageToClients(disconnectingMessage));
                _tcpListener.Stop();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stringVal"></param>
        /// <returns></returns>
        private int ParseStringToInt(string stringVal)
        {
            int.TryParse(stringVal, out var intVal);
            return intVal;
        }
    }
}