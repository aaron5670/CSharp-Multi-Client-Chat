using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace _03_ChatClientWPF
{
    public partial class MainWindow
    {
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void AddMessage(string message)
        {
            Dispatcher.Invoke(() => listChats.Items.Add(message));
        }

        private async void btnConnectDisconnect_Click(object sender, RoutedEventArgs e)
        {
            var clientName = txtNameClient.Text;
            var ipAddress = txtIPServer.Text;
            var port = txtPort.Text;

            if (DataValidator(clientName, ipAddress, port, txtBufferSize.Text))
            {
                if ((string) btnConnect.Content == "Connect")
                {
                    var bufferSize = ParseStringToInt(txtBufferSize.Text);
                    btnConnect.Content = "Disconnect";
                    btnConnect.IsEnabled = false;
                    txtNameClient.IsEnabled = false;
                    txtIPServer.IsEnabled = false;
                    txtPort.IsEnabled = false;
                    txtBufferSize.IsEnabled = false;
                    AddMessage("[CLIENT]: ⏳ Connecting...");
                    await CreateConnection(clientName, bufferSize);
                    txtMessage.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnConnect.IsEnabled = true;
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

        private async Task CreateConnection(string clientName, int bufferSize)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(txtIPServer.Text, 9000);
                _networkStream = _tcpClient.GetStream();

                await Task.Run(() => SendConnectionData(clientName));
                await Task.Run(() => ReceiveData(bufferSize));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                AddMessage("[CLIENT]: ❌ Could not create a connection with the server.");
                btnConnect.Content = "Connect";
                txtNameClient.IsEnabled = true;
                txtIPServer.IsEnabled = true;
                txtPort.IsEnabled = true;
                txtBufferSize.IsEnabled = true;
            }
        }

        private async void SendConnectionData(string name)
        {
            try
            {
                if (!_networkStream.CanWrite) return;

                var message = "";
                message += name;
                message += "CONNECT~";
                var clientMessageByteArray = Encoding.ASCII.GetBytes(message);
                await _networkStream.WriteAsync(clientMessageByteArray, 0, clientMessageByteArray.Length);
            }
            catch
            {
                MessageBox.Show("❌ Can't connect to the server, try again later!", "Client");
            }
        }

        private async void ReceiveData(int bufferSize)
        {
            var buffer = new byte[bufferSize];
            _networkStream = _tcpClient.GetStream();

            while (_networkStream.CanRead)
            {
                var message = "";

                while (message.IndexOf("~") < 0)
                {
                    var bytes = await _networkStream.ReadAsync(buffer, 0, bufferSize);
                    message = Encoding.ASCII.GetString(buffer, 0, bytes);
                }

                message = message.Remove(message.Length - 1);
                AddMessage(message);
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
            if (_networkStream.CanWrite)
            {
                var data = $"{clientName}: {message}MESSAGE~";
                var clientMessageByteArray = Encoding.ASCII.GetBytes(data);
                await _networkStream.WriteAsync(clientMessageByteArray, 0, clientMessageByteArray.Length);
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
            var allowedRegex = new Regex("[^~]+");
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