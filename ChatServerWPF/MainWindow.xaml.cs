using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace _03_ChatServerWPF
{
    public partial class MainWindow
    {
        // Stap 3:
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;

        //Works
        private TcpListener _tcpListener;
        private bool _serverStarted;
        private List<TcpClient> _clientList = new List<TcpClient>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void AddMessage(string message)
        {
            this.Dispatcher.Invoke(() => listChats.Items.Add(message));
        }

        private async void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if ((string) btnStartStop.Content == "Start")
            {
                btnStartStop.Content = "Stop";
                var serverPort = ParseStringToInt(serverPortValue.Text);
                var serverBufferSize = ParseStringToInt(serverBufferSizeValue.Text);
                await StartServer(serverPort, serverBufferSize);
            }
            else
            {
                btnStartStop.Content = "Start";
                StopServer();
            }
        }

        private async Task StartServer(int serverPort, int serverBufferSize)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, serverPort);
                _tcpListener.Start();

                _serverStarted = true;

                AddMessage($"[SERVER]: Started on port {serverPort.ToString()}");

                while (_serverStarted)
                {
                    Debug.WriteLine("Waiting for client...");
                    _tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    AddMessage("[SERVER]: Client connected!");
                    _clientList.Add(_tcpClient);
                    Debug.WriteLine("Client connected");
                    await Task.Run(() => ReceiveData(_tcpClient, serverBufferSize));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                AddMessage("[SERVER]: Another server is already running!");
            }
        }

        // private void Listen()
        // {
        //     Task.Run(async () =>
        //     {
        //         var client = await tcpListener.AcceptTcpClientAsync();
        //         clientList.Add(client);
        //         AddMessage("[SERVER]: Client joined!");
        //     });
        // }

        // private async Task Listen()
        // {
        //     while (serverStarted)
        //     {
        //         tcpClient = await tcpListener.AcceptTcpClientAsync();
        //         clientList.Add(tcpClient);
        //         //Task.Run(() => ReceiveData(tcpClient, ParseStringToInt("1024")));
        //     }
        // }

        private void StopServer()
        {
            try
            {
                AddMessage("[SERVER]: Stopped");
                _tcpListener.Stop();
            }
            catch (Exception e)
            {
                AddMessage(e.Message);
            }
        }

        private async void ReceiveData(TcpClient tcpClient, int bufferSize)
        {
            var buffer = new byte[bufferSize];
            var networkStream = tcpClient.GetStream();

            const string connectSignal = "CONNECT~";
            const string messageSignal = "MESSAGE~";
            const string disconnectSignal = "DISCONNECT~";

            while (networkStream.CanRead)
            {
                var incomingData = "";
                string message = "";

                while (incomingData.IndexOf("~") < 0)
                {
                    Debug.WriteLine("✅ Incoming message");
                    var bytes = await networkStream.ReadAsync(buffer, 0, bufferSize);
                    message = Encoding.ASCII.GetString(buffer, 0, bytes);
                    incomingData += message;
                    Debug.WriteLine("message: " + message);
                }

                /***
                 * New client connected
                 */
                if (incomingData.EndsWith(connectSignal))
                {
                    var clientName = incomingData.Remove(incomingData.Length - connectSignal.Length);
                    AddClientToClientList(clientName);
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

                Debug.WriteLine("Message: " + incomingData);
            }
        }

        private void AddClientToClientList(string clientName)
        {
            if (_clientList.Count == 1)
            {
                Dispatcher.Invoke((() => listClients.Items.RemoveAt(0)));
                Dispatcher.Invoke((() => listClients.Items.Add(clientName)));
            }
            else
            {
                Dispatcher.Invoke((() => listClients.Items.Add(clientName)));
            }
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

        private int ParseStringToInt(string stringVal)
        {
            int.TryParse(stringVal, out var intVal);
            return intVal;
        }
    }
}
