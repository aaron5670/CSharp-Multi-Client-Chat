using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace _03_ChatServerWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Stap 3:
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private Thread thread;

        //Works
        private TcpListener tcpListener;
        private Boolean serverStarted;
        private List<TcpClient> clientList = new List<TcpClient>();

        public MainWindow()
        {
            InitializeComponent();
        }

        // Stap 5:
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
                tcpListener = new TcpListener(IPAddress.Any, serverPort);
                tcpListener.Start();

                serverStarted = true;

                AddMessage($"[SERVER]: Started on port {serverPort.ToString()}");

                while (serverStarted)
                {
                    Debug.WriteLine("Waiting for client...");
                    tcpClient = await tcpListener.AcceptTcpClientAsync();
                    AddMessage("[SERVER]: Client connected!");
                    clientList.Add(tcpClient);
                    Debug.WriteLine("Client connected");
                    await Task.Run(() => ReceiveData(tcpClient, ParseStringToInt("1024")));
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
                tcpListener.Stop();
            }
            catch (Exception e)
            {
                AddMessage(e.Message);
            }
        }

        private async void ReceiveData(TcpClient tcpClient, int bufferSize)
        {
            var buffer = new byte[bufferSize];
            networkStream = tcpClient.GetStream();

            const string connectSignal = "~CONNECT";
            const string messageSignal = "~MESSAGE";
            const string disconnectSignal = "~DISCONNECT";

            while (networkStream.CanRead)
            {
                var message = "";

                while (message.IndexOf("~") < 0)
                {
                    Debug.WriteLine("✅ Incoming message");
                    var bytes = await networkStream.ReadAsync(buffer, 0, bufferSize);
                    message = Encoding.ASCII.GetString(buffer, 0, bytes);
                    Debug.WriteLine(message);
                }

                if (message.EndsWith(connectSignal))
                {
                    var clientName = message.Remove(message.Length - connectSignal.Length);
                    AddClientToClientList(clientName);
                    await SendMessageToClients($"[SERVER]: {clientName} connected!~");
                }

                if (message.EndsWith(messageSignal))
                {
                    message = message.Remove(message.Length - messageSignal.Length);
                    AddMessage(message);
                    await SendMessageToClients($"{message}~");
                }

                Debug.WriteLine("Message: " + message);
            }
        }

        private void AddClientToClientList(string clientName)
        {
            if (clientList.Count == 1)
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
            if (clientList.Count > 0)
            {
                foreach (var client in clientList)
                {
                    networkStream = client.GetStream();
                    if (!networkStream.CanRead) continue;

                    var serverMessageByteArray = Encoding.ASCII.GetBytes(message);
                    await networkStream.WriteAsync(serverMessageByteArray, 0, serverMessageByteArray.Length);
                    Debug.WriteLine("Message send a client (must be multiple execute");
                }
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            var message = txtMessage.Text;

            var buffer = Encoding.ASCII.GetBytes(message);
            networkStream.Write(buffer, 0, buffer.Length);

            await SendMessageToClients($"[SERVER]: {message}~");
            
            AddMessage($"[SERVER]: {message}~");
            txtMessage.Clear();
            txtMessage.Focus();
        }

        private int ParseStringToInt(string stringVal)
        {
            int.TryParse(stringVal, out var intVal);
            return intVal;
        }
    }
}