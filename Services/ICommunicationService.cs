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
        private TcpClient _tcpServerClient;  // ✅ 保存 Server 端接受的客户端
        private TcpClient _tcpClient;        // 保存 Client 模式的连接
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
            if (IsActive)
            {
                _logger.Warning("[Start] 服务已在运行，先停止");
                Stop();
            }
            _currentProtocol = protocol;
            _currentRole = role;
            _targetIp = ip;
            _targetPort = port;
            _cts = new CancellationTokenSource();
           
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
                //if (protocol == CommProtocol.TCP && role == CommRole.Server)
                //{
                //    IsActive = true;
                //    ConnectionStatusChanged?.Invoke(true);
                //    _logger.Information("[TCP Server] 服务已启动，监听中...");
                //}
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
                    //Server 模式：通过保存的客户端发送
                    if (_currentRole == CommRole.Server)
                    {
                        if (_tcpServerClient != null && _tcpServerClient.Connected)
                        {
                            await _tcpServerClient.GetStream().WriteAsync(data, 0, data.Length, _cts.Token);
                            _logger.Information("[TCP Server] 已发送消息");
                        }
                        else
                        {
                            _logger.Error("发送失败：TCP Server 客户端连接不可用");
                        }
                    }
                    //Client 模式：通过客户端连接发送
                    else
                    {
                        if (_tcpClient != null && _tcpClient.Connected)
                        {
                            await _tcpClient.GetStream().WriteAsync(data, 0, data.Length, _cts.Token);
                            _logger.Information("[TCP Client] 已发送消息");
                        }
                        else
                        {
                            _logger.Error("发送失败：TCP Client 未连接");
                        }
                    }
                }
                else //UDP
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
            if (!IsActive) return;
            IsActive = false;
            ConnectionStatusChanged?.Invoke(false);
            _cts?.Cancel();

            _tcpServer?.Stop();
            _tcpClient?.Close();
            _tcpServerClient?.Close();  //关闭Server端的客户端连接
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
                        string clientAddr = client.Client.RemoteEndPoint.ToString();
                        _logger.Information("[TCP Server] 客户端已接入: {ClientAddr}", clientAddr);
                        _tcpServerClient = client;
                        if (!IsActive)
                        {
                            IsActive = true;
                            ConnectionStatusChanged?.Invoke(true);
                            _logger.Information("[TCP Server] 客户端已连接，标记为活跃连接");
                        }
                        _ = Task.Run(() => HandleTcpConnection(client, _cts.Token));
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Information("[TCP Server] 服务已停止");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "[TCP Server] 异常");
                        break;
                    }
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
                                IsActive = true;
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

                    await Task.Delay(5000, _cts.Token);
                }
            }, _cts.Token);
        }

        private async Task HandleTcpClientReceive(CancellationToken token)
        {
            try
            {
                var stream = _tcpClient.GetStream();
                byte[] buffer = new byte[4096];
                while (!token.IsCancellationRequested && _tcpClient.Connected)
                {
                    try
                    {
                        int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                        if (read == 0)
                        {
                            _logger.Information("[TCP Client] 服务器主动断开连接");
                            break;
                        }

                        string message = Encoding.UTF8.GetString(buffer, 0, read);
                        MessageReceived?.Invoke("Server", message);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "[TCP Client] 读取数据出错");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[TCP Client] 接收处理异常");
            }
            finally
            {
                //断开时触发事件
                IsActive = false;
                ConnectionStatusChanged?.Invoke(false);
                _logger.Information("[TCP Client] 与服务器断开连接");
            }
        }

        private async Task HandleTcpConnection(TcpClient client, CancellationToken token)
        {
            string remote = client.Client.RemoteEndPoint.ToString();
            _logger.Debug($"[TCP Server] 客户端接入: {remote}");
            try
            {
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
            }
            finally
            {
                _tcpServerClient = null;
                IsActive = false;
                ConnectionStatusChanged?.Invoke(false);
                RaiseLogMessage($"[TCP Server] 客户端已断开: {remote}");
                _logger.Debug($"[TCP Server] 客户端断开: {remote}");
            }

          
        }

        private void RaiseLogMessage(string message)
        {
            LogMessage?.Invoke(message);
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
