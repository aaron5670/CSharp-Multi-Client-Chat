using System;
using System.Collections.Generic;
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
        private CancellationTokenSource cancellation;

        public MainWindow()
        {
            InitializeComponent();
        }

        // Stap 5:
        private void AddMessage(string message)
        {
            this.Dispatcher.Invoke(() => listChats.Items.Add(message));
        }

        // Stap 7:
        private void ReceiveData()
        {
            int bufferSize = 1024;
            string message = "";
            byte[] buffer = new byte[bufferSize];

            networkStream = tcpClient.GetStream();

            AddMessage("Connected!");

            while (true)
            {
                int readBytes = networkStream.Read(buffer, 0, bufferSize);
                message = Encoding.ASCII.GetString(buffer, 0, readBytes);

                if (message == "bye")
                    break;

                AddMessage(message);
            }

            // Verstuur een reactie naar de client (afsluitend bericht)
            buffer = Encoding.ASCII.GetBytes("bye");
            networkStream.Write(buffer, 0, buffer.Length);

            // cleanup:
            networkStream.Close();
            tcpClient.Close();

            AddMessage("Connection closed");
        }

        private void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if ((string) btnStartStop.Content == "Start")
            {
                btnStartStop.Content = "Stop";
                StartServer();
            }
            else
            {
                btnStartStop.Content = "Start";
                StopServer();
            }
        }

        private void StartServer()
        {
            try
            {
                var serverPort = ParseStringToInt(serverPortValue.Text);
                var serverBufferSize = ParseStringToInt(serverBufferSizeValue.Text);

                serverStarted = true;

                tcpListener = new TcpListener(IPAddress.Any, serverPort);
                tcpListener.Start();
                
                AddMessage($"[SERVER]: Started on port {serverPort.ToString()}");
                Listen();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                AddMessage("[SERVER]: Another server is already running!");
            }
        }

        private void Listen()
        {
            Task.Run(async () =>
            {
                while (serverStarted)
                {
                    var client = await tcpListener.AcceptTcpClientAsync();
                    clientList.Add(client);
                    AddMessage("[SERVER]: Client joined!");
                }
            });
        }

        private void StopServer()
        {
            try
            {
                serverStarted = false;
                AddMessage("[SERVER]: Stopped");
                tcpListener.Stop();
            }
            catch (Exception e)
            {
                AddMessage(e.Message);
            }
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            string message = txtMessage.Text;

            byte[] buffer = Encoding.ASCII.GetBytes(message);
            networkStream.Write(buffer, 0, buffer.Length);

            AddMessage(message);
            txtMessage.Clear();
            txtMessage.Focus();
        }

        private int ParseStringToInt(string stringVal)
        {
            int intVal;
            int.TryParse(stringVal, out intVal);

            return intVal;
        }
    }
}