using aoi_common.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
        event Action<bool> ConnectionStatusChanged;
    }

    public class CommunicationService : ICommunicationService, IDisposable
    {
        private ILogger _logger;
        private TcpListener _tcpServer;
        private TcpClient _tcpClient;
        private UdpClient _udpClient;
        private IPEndPoint _remoteEndPoint;
        private CancellationTokenSource _cts;
        private CommProtocol _currentProtocol;
        private CommRole _currentRole;
        private string _targetIp;
        private int _targetPort;
        private bool _isRunning;

        public bool IsActive { get; private set; }

        public event Action<string, string> MessageReceived;
        public event Action<string> LogMessage;
        public event Action<bool> ConnectionStatusChanged;


        public CommunicationService(ILogger logger)
        {
            _logger = logger;
        }

        public void Start(CommProtocol protocol, CommRole role, string ip, int port)
        {
            if (IsActive) Stop();
            _currentProtocol = protocol;
            _currentRole = role;
            _targetIp = ip;
            _targetPort = port;
            _cts = new CancellationTokenSource();
            IsActive = true;
            try
            {
                if (protocol == CommProtocol.TCP)
                {
                    if (role == CommRole.Server) StartTcpServer(port);
                    else StartTcpClientWithReconnection(ip, port);
                }
                else
                {
                    StartUdp(role, ip, port);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"启动异常: {ex.Message}");

                Stop();
            }

        }

        public async Task SendAsync(string message)
        {
            if (!IsActive) return;

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                if (_currentProtocol == CommProtocol.TCP)
                {
                    if (_tcpClient != null && _tcpClient.Connected)
                    {
                        await _tcpClient.GetStream().WriteAsync(data, 0, data.Length, _cts.Token);
                    }
                    else _logger.Error("发送失败：TCP 未连接");
                }
                else // UDP
                {
                    if (_udpClient != null && _remoteEndPoint != null)
                    {
                        await _udpClient.SendAsync(data, data.Length, _remoteEndPoint);
                    }
                    else _logger.Error("发送失败：UDP 目标未指定（请等待对方先发消息或检查配置）");
                }
            }
            catch (Exception ex) { _logger.Error($"发送异常: {ex.Message}"); }
        }

        public void Stop()
        {
            IsActive = false;
            _cts?.Cancel();

            _tcpServer?.Stop();
            _tcpClient?.Close();
            _udpClient?.Close();

            _tcpServer = null;
            _tcpClient = null;
            _udpClient = null;

            _logger.Information("通讯服务已停止");
        }

        #region TCP 

        private void StartTcpServer(int port)
        {
            _tcpServer = new TcpListener(IPAddress.Any, port);
            _tcpServer.Start();
            _logger.Debug($"[TCP Server] 正在监听端口: {port}");

            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await _tcpServer.AcceptTcpClientAsync();
                        _ = Task.Run(() => HandleTcpConnection(client, _cts.Token));
                    }
                    catch { break; }
                }
            }, _cts.Token);
        }

        private void StartTcpClientWithReconnection(string ip, int port)
        {
            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (_tcpClient == null || !_tcpClient.Connected)
                        {
                            _logger.Debug($"[TCP Client] 尝试连接至 {ip}:{port}...");
                            _tcpClient?.Close();
                            _tcpClient = new TcpClient();

                            // 设置连接超时
                            var connectTask = _tcpClient.ConnectAsync(ip, port);
                            if (await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask)
                            {
                                await connectTask;
                                _logger.Information("[TCP Client] 连接成功");
                                await HandleTcpClientReceive(_cts.Token);
                            }
                            else
                            {
                                _logger.Error("[TCP Client] 连接超时，5秒后重试...");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[TCP Client] 错误: {ex.Message}，5秒后重试...");
                    }

                    await Task.Delay(5000, _cts.Token); // 重连间隔
                }
            }, _cts.Token);
        }

        private async Task HandleTcpClientReceive(CancellationToken token)
        {
            var stream = _tcpClient.GetStream();
            byte[] buffer = new byte[4096];
            while (!token.IsCancellationRequested && _tcpClient.Connected)
            {
                try
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read == 0) break;  // 连接断开

                    string message = Encoding.UTF8.GetString(buffer, 0, read);
                    MessageReceived?.Invoke("Server", message);
                }
                catch { break; }
            }
            IsActive = false;
            ConnectionStatusChanged?.Invoke(false);
            _logger.Information("[TCP Client] 与服务器断开连接");
        }

        private async Task HandleTcpConnection(TcpClient client, CancellationToken token)
        {
            string remote = client.Client.RemoteEndPoint.ToString();
            _logger.Debug($"[TCP Server] 客户端接入: {remote}");
            using (client)
            using (var stream = client.GetStream())
            {
                byte[] buffer = new byte[4096];
                while (!token.IsCancellationRequested && client.Connected)
                {
                    try
                    {
                        int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                        if (read == 0) break;
                        string message = Encoding.UTF8.GetString(buffer, 0, read);
                        MessageReceived?.Invoke(remote, message);
                    }
                    catch { break; }
                }
            }
            _logger.Debug($"[TCP Server] 客户端断开: {remote}");
        }

        #endregion

        #region  UDP
        private void StartUdp(CommRole role, string ip, int port)
        {
            if (role == CommRole.Server)
            {
                _udpClient = new UdpClient(port);
                _logger.Debug($"[UDP Server] 监听端口: {port}");
            }
            else
            {
                _udpClient = new UdpClient();
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                _logger.Debug($"[UDP Client] 目标已指向: {ip}:{port}");
            }

            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync();
                        if (_currentRole == CommRole.Server) _remoteEndPoint = result.RemoteEndPoint;

                        MessageReceived?.Invoke(result.RemoteEndPoint.ToString(), Encoding.UTF8.GetString(result.Buffer));
                    }
                    catch { break; }
                }
            }, _cts.Token);
        }

        public void Dispose() => Stop();


        #endregion



    }
}
