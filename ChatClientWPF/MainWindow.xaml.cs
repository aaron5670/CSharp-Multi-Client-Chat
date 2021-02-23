using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
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

namespace _03_ChatClientWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Stap 3:
        TcpClient tcpClient;
        NetworkStream networkStream;
        Thread thread;

        public MainWindow()
        {
            InitializeComponent();
        }

        // Stap 5:
        private void AddMessage(string message)
        {
            Dispatcher.Invoke(() => listChats.Items.Add(message));
        }

        private async void ReceiveData(int bufferSize)
        {
            var buffer = new byte[bufferSize];
            networkStream = tcpClient.GetStream();

            while (networkStream.CanRead)
            {
                var message = "";
                
                while (message.IndexOf("~") < 0)
                {
                    var bytes = await networkStream.ReadAsync(buffer, 0, bufferSize);
                    message = Encoding.ASCII.GetString(buffer, 0, bytes);
                }

                message = message.Remove(message.Length - 1);
                AddMessage(message);
            }
        }

        private async void btnConnectDisconnect_Click(object sender, RoutedEventArgs e)
        {
            var clientName = txtNameClient.Text;
            var ipAddress = txtIPServer.Text;
            var port = txtPort.Text;
            var bufferSize = txtBufferSize.Text;

            // tcpClient = new TcpClient();
            // await tcpClient.ConnectAsync(txtIPServer.Text, 9000);
            // AddMessage("[CLIENT]: Connected!");

            if (DataValidator(clientName, ipAddress, port, bufferSize))
            {
                if ((string) btnConnect.Content == "Connect")
                {
                    btnConnect.Content = "Disconnect";
                    txtNameClient.IsEnabled = false;
                    txtIPServer.IsEnabled = false;
                    txtPort.IsEnabled = false;
                    txtBufferSize.IsEnabled = false;
                    AddMessage("[CLIENT]: ⏳ Connecting...");
                    await CreateConnection(clientName);
                    txtMessage.IsEnabled = true;
                    btnSend.IsEnabled = true;
                }
                else
                {
                    btnConnect.Content = "Connect";
                    txtNameClient.IsEnabled = true;
                    txtIPServer.IsEnabled = true;
                    txtPort.IsEnabled = true;
                    txtBufferSize.IsEnabled = true;
                    txtMessage.IsEnabled = false;
                    btnSend.IsEnabled = false;
                }
            }
            else
            {
                AddMessage("[CLIENT]: ❌ Can't connect, data is invalid!");
            }
        }

        private async Task CreateConnection(string clientName)
        {
            try
            {
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(txtIPServer.Text, 9000);
                networkStream = tcpClient.GetStream();

                await Task.Run(() => SendConnectionData(clientName));
                await Task.Run(() => ReceiveData(ParseStringToInt("1024")));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                MessageBox.Show("Could not create a connection with the server", "Connection error");
            }
        }

        private async void SendConnectionData(string name)
        {
            try
            {
                if (!networkStream.CanWrite) return;

                var message = "";
                message += name;
                message += "~CONNECT";
                var clientMessageByteArray = Encoding.ASCII.GetBytes(message);
                await networkStream.WriteAsync(clientMessageByteArray, 0, clientMessageByteArray.Length);
            }
            catch
            {
                MessageBox.Show("❌ Can't connect to the server, try again later!", "Client");
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var message = txtMessage.Text;
                if (MessageValidator(message))
                {
                    await SendMessageToServer(txtNameClient.Text, txtMessage.Text);
                }
                else throw new FormatException();
            }
            catch (FormatException)
            {
                MessageBox.Show("❌ Message has invalid characters, try again!", "Client");
            }
            catch
            {
                MessageBox.Show("❌ Can't send message to server, try again later!", "Client");
            }
        }

        private async Task SendMessageToServer(string clientName, string message)
        {
            if (networkStream.CanWrite)
            {
                var data = $"{clientName}: {message}~MESSAGE";
                var clientMessageByteArray = Encoding.ASCII.GetBytes(data);
                await networkStream.WriteAsync(clientMessageByteArray, 0, clientMessageByteArray.Length);
            }

            txtMessage.Clear();
            txtMessage.Focus();
        }

        private bool DataValidator(string clientName, string ipAddress, string port, string bufferSize)
        {
            var allowedRegex = new Regex("^[a-zA-Z0-9 ]*$");
            if (!allowedRegex.IsMatch(clientName) || clientName.Length == 0)
                return false;

            if (!IPAddress.TryParse(ipAddress, out var ip))
                return false;

            const int maxPortNumber = 65535;
            if (!port.All(char.IsDigit) && ParseStringToInt(port) <= maxPortNumber)
                return false;

            if (!bufferSize.All(char.IsDigit) || bufferSize.Length == 0)
                return false;

            return true;
        }

        private bool MessageValidator(string message)
        {
            var allowedRegex = new Regex("^[a-zA-Z0-9 ]*$");
            return (allowedRegex.IsMatch(message) || message.Length == 0);
        }

        private int ParseStringToInt(string stringVal)
        {
            int intVal;
            int.TryParse(stringVal, out intVal);

            return intVal;
        }
    }
}