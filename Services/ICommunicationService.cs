using aoi_common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Services
{
    

    public interface ICommunicationService
    {
        bool IsActive { get; }
        void Start(CommProtocol protocol, CommRole role, string ip, int port);
        void Stop();
        Task SendAsync(string message);

        event Action<string, string> MessageReceived;
        event Action<string> LogMessage;
    }

    public class CommunicationService : ICommunicationService
    {
        private TcpListener _tcpServer;
        private TcpClient _tcpClient;
        private UdpClient _udpClient;
        private IPEndPoint _remoteEndPoint;
        private bool _isRunning;

        public bool IsActive { get; private set; }

        public event Action<string, string> MessageReceived;
        public event Action<string> LogMessage;

        public void Start(CommProtocol protocol, CommRole role, string ip, int port)
        {
            if(IsActive) Stop();
            _isRunning =true;
            try
            {
                if (protocol == CommProtocol.TCP)
                {
                    if (role == CommRole.Server) StartTcpServer(port);
                    else StartTcpClient(ip, port);
                }
                else
                {
                    StartUdp(role, ip, port);
                }
                IsActive =true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"启动失败: {ex.Message}"); Stop();
            }
        }

        public async Task SendAsync(string message)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(message);
                if (_tcpClient != null && _tcpClient.Connected) await _tcpClient.GetStream().WriteAsync(data, 0, data.Length);
                else if (_udpClient != null && _remoteEndPoint != null) await _udpClient.SendAsync(data, data.Length, _remoteEndPoint);
                else LogMessage?.Invoke("发送失败：未建立连接或未指定目标");
            }
            catch (Exception ex) { LogMessage?.Invoke($"发送异常: {ex.Message}"); }
        }

        public void Stop()
        {
            _isRunning = false; IsActive = false;
            _tcpServer?.Stop(); _tcpClient?.Close(); _udpClient?.Close();
            LogMessage?.Invoke("通讯已停止");
        }

        #region TCP 

        private void StartTcpServer(int port)
        {
            _tcpServer = new TcpListener(IPAddress.Any, port);
            _tcpServer.Start();
            LogMessage?.Invoke($"[TCP Server] 启动监听: {port}");
            Task.Run(async () => {
                while (_isRunning)
                {
                    try
                    {
                        var client = await _tcpServer.AcceptTcpClientAsync();
                        _ = Task.Run(() => HandleTcpConnection(client));
                    }
                    catch { break; }
                }
            });
        }

        private void StartTcpClient(string ip, int port)
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(ip, port);
            LogMessage?.Invoke($"[TCP Client] 已连接至 {ip}:{port}");
            Task.Run(async () => {
                var stream = _tcpClient.GetStream();
                byte[] buffer = new byte[1024];
                while (_isRunning && _tcpClient.Connected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    MessageReceived?.Invoke("Server", Encoding.ASCII.GetString(buffer, 0, read));
                }
                LogMessage?.Invoke("[TCP Client] 服务端已断开");
            });
        }

        private async Task HandleTcpConnection(TcpClient client)
        {
            string remote = client.Client.RemoteEndPoint.ToString();
            LogMessage?.Invoke($"[TCP Server] 客户端连接: {remote}");
            using (var stream = client.GetStream())
            {
                byte[] buffer = new byte[1024];
                while (_isRunning && client.Connected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    MessageReceived?.Invoke(remote, Encoding.ASCII.GetString(buffer, 0, read));
                }
            }
            LogMessage?.Invoke($"[TCP Server] 连接断开: {remote}");
        }

        #endregion

        #region  UDP
        private void StartUdp(CommRole role, string ip, int port)
        {
            if (role == CommRole.Server)
            {
                _udpClient = new UdpClient(port); 
                LogMessage?.Invoke($"[UDP Server] 开始监听端口: {port}");
            }
            else
            {
                _udpClient = new UdpClient();   
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                LogMessage?.Invoke($"[UDP Client] 目标设定为: {ip}:{port}");
            }
            Task.Run(async () => {
                while (_isRunning)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync();
                        MessageReceived?.Invoke(result.RemoteEndPoint.ToString(), Encoding.ASCII.GetString(result.Buffer));
                    }
                    catch { break; }
                }
            });
        }

        #endregion

       
      
    }
}
