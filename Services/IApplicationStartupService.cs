using aoi_common.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Services
{
    public interface IApplicationStartupService
    {
        Task InitializeAsync();
        Task ShutdownAsync();
    }

    public class ApplicationStartupService : IApplicationStartupService
    {
        private readonly IDetectionLogicService _detectionLogicService;
        private readonly ICommunicationService _communicationService;
        private readonly ICameraConfigService _cameraConfigService;
        private readonly IVisionService _visionService;
        private readonly IParametersConfigService _configService;
        private readonly ILogger _logger;

        public ApplicationStartupService(ICameraConfigService cameraConfigService, IVisionService visionService,
            ICommunicationService communicationService, IParametersConfigService configService,
            ILogger logger, IDetectionLogicService detectionLogicService)
        {
            _cameraConfigService = cameraConfigService;
            _visionService = visionService;
            _logger = logger;
            _detectionLogicService = detectionLogicService;
            _communicationService = communicationService;
            _configService = configService;
        }

        public async Task InitializeAsync()
        {
            _logger.Information("开始应用初始化...");

            try
            {
                //加载通讯服务
                await InitializeCommunicationAsync();
                //加载相机配置  //视觉vpp文件
                await Task.WhenAll(InitializeCameraAsync(), InitializeVisionServiceAsync());
               
                _logger.Information("应用初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "应用初始化失败");
                throw;
            }
        }

        private async Task InitializeCommunicationAsync()
        {
            _logger.Information("初始化通讯服务...");

            try
            {

                //CommProtocol protocol = CommProtocol.TCP;
                //CommRole role = CommRole.Server;
                //string ip = "127.0.0.1";
                //int port = 5000;
                //_logger.Information("通讯配置 - 协议: {Protocol}, 角色: {Role}, IP: {IP}, 端口: {Port}",
                //    protocol, role, ip, port);

                CommProtocol protocol = GetCommProtocol();
                CommRole role = GetCommRole();
                string ip = GetCommIp();
                int port = GetCommPort();
                _logger.Information("通讯配置-协议: {Protocol}, 角色: {Role}, IP: {IP}, 端口: {Port}", protocol, role, ip, port);

                _communicationService.Start(protocol, role, ip, port);
                _communicationService.MessageReceived += (sender, message) =>
                {
                    _logger.Information("接收来自 {Sender} 的消息: {Message}", sender, message);
                    try
                    {
                        _detectionLogicService.ProcessPlcData(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "处理消息失败");
                    }
                };

                _logger.Information("通讯服务已启动，等待连接...");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "初始化通讯服务失败");
                _logger.Warning("通讯服务启动失败，但应用将继续运行");
            }
        }

        private async Task InitializeCameraAsync()
        {
            _logger.Information("开始初始化相机配置...");

            try
            {
                _cameraConfigService.GetOrCreateAcqFifoTool();
                string defaultConfigPath = _cameraConfigService.GetDefaultConfigPath();
                bool configLoaded = await _cameraConfigService.LoadConfigAsync(defaultConfigPath);

                if (configLoaded)
                {
                    _logger.Information("相机配置已从本地文件加载");
                }
                else
                {
                    _logger.Information("本地相机配置不存在，使用默认配置");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "初始化相机配置失败");
                throw;
            }
        }

        private async Task InitializeVisionServiceAsync()
        {
            _logger.Information("开始初始化VisionService...");

            try
            {
                //{
                //    string vppPath = System.IO.Path.Combine(
                //        AppDomain.CurrentDomain.BaseDirectory,
                //        "Resources",
                //        "toolblock.vpp");
                string vppPath = "C:\\Users\\xuze\\Desktop\\testvpp.vpp";
                await _visionService.InitialAsync(vppPath);

                if (_visionService.IsInitialized)
                {
                    _logger.Information("VisionService初始化成功");
                }
                else
                {
                    _logger.Warning("VisionService初始化失败");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "初始化VisionService失败");
                throw;
            }
        }


        private CommProtocol GetCommProtocol()
        {
            try
            {
                string protocolStr = _configService.GetString("Communication", "Protocol", "TCP");
                if (Enum.TryParse<CommProtocol>(protocolStr, out var protocol))
                {
                    return protocol;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "读取通讯协议配置失败，使用默认值");
            }
            return CommProtocol.TCP;
        }

        private CommRole GetCommRole()
        {
            try
            {
                string roleStr = _configService.GetString("Communication", "Role", "Server");
                if (Enum.TryParse<CommRole>(roleStr, out var role))
                {
                    return role;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "读取通讯角色配置失败，使用默认值");
            }
            return CommRole.Server;
        }

        private string GetCommIp()
        {
            try
            {
                return _configService.GetString("Communication", "IP", "127.0.0.1");
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "读取IP配置失败，使用默认值");
                return "127.0.0.1";
            }
        }

        private int GetCommPort()
        {
            try
            {
                return _configService.GetInt("Communication", "Port", 5000);

            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "读取端口配置失败，使用默认值");
            }
            return 5000;
        }

        public async Task ShutdownAsync()
        {
            _logger.Information("应用关闭，保存相机配置...");

            try
            {
                string defaultConfigPath = _cameraConfigService.GetDefaultConfigPath();
                if (_cameraConfigService.CurrentCogAcqFifoTool.Operator == null)
                    return;
                await _cameraConfigService.SaveConfigAsync(defaultConfigPath);
                _logger.Information("相机配置已保存");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "保存相机配置失败");
            }
        }
    }
}
