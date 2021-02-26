using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace _03_ChatClientWPF
{
    /// <summary>
    /// TCP chat client
    /// </summary>
    public partial class MainWindow
    {
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
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
        /// This method will handle the connect and disconnect button functionality
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnConnectDisconnect_Click(object sender, RoutedEventArgs e)
        {
            var clientName = txtNameClient.Text;
            var ipAddress = txtIPServer.Text;

            if (DataValidator(clientName, ipAddress, txtPort.Text, txtBufferSize.Text))
            {
                if ((string) btnConnect.Content == "Connect")
                {
                    var bufferSize = ParseStringToInt(txtBufferSize.Text);
                    var port = ParseStringToInt(txtPort.Text);
                    btnConnect.Content = "Disconnect";
                    btnConnect.IsEnabled = false;
                    txtNameClient.IsEnabled = false;
                    txtIPServer.IsEnabled = false;
                    txtPort.IsEnabled = false;
                    txtBufferSize.IsEnabled = false;
                    AddMessage("[CLIENT]: ⏳ Connecting...");
                    await CreateConnection(ipAddress, port, clientName, bufferSize);
                    txtMessage.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnConnect.IsEnabled = true;
                }
                else
                {
                    await DisconnectClient();
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

        /// <summary>
        /// This method creates the TCP connection
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="port"></param>
        /// <param name="clientName"></param>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        private async Task CreateConnection(string ipAddress, int port, string clientName, int bufferSize)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ipAddress, port);
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

        /// <summary>
        /// This method will send the connection data
        /// </summary>
        /// <param name="name"></param>
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

        /// <summary>
        /// This method reads all incoming data / messages
        /// </summary>
        /// <param name="bufferSize"></param>
        private async void ReceiveData(int bufferSize)
        {
            var buffer = new byte[bufferSize];
            var networkStream = _tcpClient.GetStream();

            while (networkStream.CanRead)
            {
                var incomingMessage = "";
                var message = "";

                try
                {
                    while (incomingMessage.IndexOf("~") < 0)
                    {
                        var bytes = await networkStream.ReadAsync(buffer, 0, bufferSize);
                        var encodedMessage = Encoding.ASCII.GetString(buffer, 0, bytes);
                        incomingMessage += encodedMessage;
                    }
                }
                catch (IOException)
                {
                    AddMessage("[SERVER]: ❌ Connection with server is lost!");
                    Dispatcher.Invoke(() => btnConnect.Content = "Connect");
                    Dispatcher.Invoke(() => txtNameClient.IsEnabled = true);
                    Dispatcher.Invoke(() => txtIPServer.IsEnabled = true);
                    Dispatcher.Invoke(() => txtPort.IsEnabled = true);
                    Dispatcher.Invoke(() => txtBufferSize.IsEnabled = true);
                    Dispatcher.Invoke(() => txtMessage.IsEnabled = false);
                    Dispatcher.Invoke(() => btnSend.IsEnabled = false);
                    break;
                }

                /***
                 * Client disconnected
                 */
                if (incomingMessage.EndsWith(ClientDisconnectSignal))
                {
                    AddMessage("[CLIENT]: ❌ Disconnected!");
                    break;
                }

                /***
                 * Server disconnected
                 */
                if (incomingMessage.EndsWith(ServerDisconnectSignal))
                {
                    message = incomingMessage.Remove(incomingMessage.Length - ServerDisconnectSignal.Length);
                    AddMessage(message);

                    Dispatcher.Invoke(() => btnConnect.Content = "Connect");
                    Dispatcher.Invoke(() => txtNameClient.IsEnabled = true);
                    Dispatcher.Invoke(() => txtIPServer.IsEnabled = true);
                    Dispatcher.Invoke(() => txtPort.IsEnabled = true);
                    Dispatcher.Invoke(() => txtBufferSize.IsEnabled = true);
                    Dispatcher.Invoke(() => txtMessage.IsEnabled = false);
                    Dispatcher.Invoke(() => btnSend.IsEnabled = false);
                    break;
                }

                message = incomingMessage.Remove(incomingMessage.Length - 1);
                AddMessage(message);
            }
        }

        /// <summary>
        /// This method will handle the send message button functionality
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="FormatException"></exception>
        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var message = txtMessage.Text;
                if (MessageIsValid(message))
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

        /// <summary>
        /// This method sends a user chat message to the server
        /// </summary>
        /// <param name="clientName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
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

        /// <summary>
        /// This method will do the disconnect functionality
        /// </summary>
        /// <returns></returns>
        private async Task DisconnectClient()
        {
            var disconnectMessage = txtNameClient.Text + ": is disconnected!";
            disconnectMessage += ClientDisconnectSignal;

            _networkStream = _tcpClient.GetStream();

            if (_networkStream.CanRead)
            {
                var clientMessageByteArray = Encoding.ASCII.GetBytes(disconnectMessage);
                await _networkStream.WriteAsync(clientMessageByteArray, 0, clientMessageByteArray.Length);
            }
        }

        /// <summary>
        /// This method will run when user closes the client (prevent a crash)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CloseClientConnection(object sender, CancelEventArgs e)
        {
            if (_tcpClient != null && _tcpClient.Connected) await DisconnectClient();
        }

        /// <summary>
        /// Data validator method validates:
        ///     - clientName
        ///     - ipAddress
        ///     - port
        ///     - bufferSize
        /// </summary>
        /// <param name="clientName"></param>
        /// <param name="ipAddress"></param>
        /// <param name="port"></param>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        private bool DataValidator(string clientName, string ipAddress, string port, string bufferSize)
        {
            var allowedRegex = new Regex("^[a-zA-Z0-9 ]*$");
            if (!allowedRegex.IsMatch(clientName) || clientName.Length == 0)
                return false;

            if (!IPAddress.TryParse(ipAddress, out var ip))
                return false;

            const int maxPortNumber = 65535;
            const int minPortNumber = 0;
            if (!port.All(char.IsDigit) || !(ParseStringToInt(port) > minPortNumber) ||
                !(ParseStringToInt(port) <= maxPortNumber))
                return false;

            if (!bufferSize.All(char.IsDigit) || ParseStringToInt(bufferSize) < 1 || bufferSize.Length == 0)
                return false;

            return true;
        }

        /// <summary>
        /// Validate a users chat message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private bool MessageIsValid(string message)
        {
            return message.Contains("~") == false && message.Length > 0;
        }

        /// <summary>
        /// Parse string to integer when possible
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