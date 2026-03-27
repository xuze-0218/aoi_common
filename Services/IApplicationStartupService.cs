using Serilog;
using System;
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
        private readonly IDetectionSessionService _detectionSessionService;
        private readonly ICommunicationService _communicationService;
        private readonly ICameraConfigService _cameraConfigService;
        private readonly IVisionService _visionService;
        private readonly ILogger _logger;

        /// <summary>
        /// 记录后台初始化任务，确保在应用关闭时可以等待其完成或安全取消
        /// </summary>
        private Task _backgroundInitializationTask;

        public ApplicationStartupService(ICameraConfigService cameraConfigService, IVisionService visionService,
            ICommunicationService communicationService, IParametersConfigService configService, ILogger logger,
            IDetectionSessionService detectionSessionService)
        {
            _cameraConfigService = cameraConfigService;
            _visionService = visionService;
            _logger = logger;
            _communicationService = communicationService;
            _detectionSessionService = detectionSessionService;
        }

        public async Task InitializeAsync()
        {
            _logger.Information("开始应用初始化");

            try
            {
                //加载通讯服务
                await InitializeCommunicationAsync();
                //加载相机配置  //加载vpp文件

                _backgroundInitializationTask = Task.Run(async () =>
                {
                    try
                    {
                        await Task.WhenAll(InitializeCameraAsync(), InitializeVisionServiceAsync());
                        _logger.Information("后台初始化完成");
                    }
                    catch (Exception ex)
                    {
                        _logger.Fatal(ex, "后台初始化失败");
                    }
                });
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "应用初始化失败");
                throw;
            }

        }

        private async Task InitializeCommunicationAsync()
        {
            _logger.Debug("初始化通讯服务");

            try
            {
                _communicationService.Start();
                _communicationService.MessageReceived += async (sender, message) =>
                {
                    _logger.Debug("接收来自 {Sender} 的消息: {Message}", sender, message);
                    try
                    {
                        var result = await _detectionSessionService.StartDetectionSessionAsync(message);
                        _logger.Information("检测会话完成: Success={Success}, Message={Message}",
                            result.IsSuccess, result.Message);
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
            _logger.Debug("开始初始化相机配置");

            try
            {
                _cameraConfigService.GetOrCreateAcqFifoTool();
                string defaultConfigPath = _cameraConfigService.GetDefaultConfigPath();
                bool configLoaded = await _cameraConfigService.LoadConfigAsync(defaultConfigPath);

                if (configLoaded)
                {
                    _logger.Debug("相机配置已从本地文件加载");
                }
                else
                {
                    _logger.Debug("本地相机配置不存在，使用默认配置");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "初始化相机配置失败");
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
            }
        }

        public async Task ShutdownAsync()
        {
            try
            {

                if (_backgroundInitializationTask != null)
                {
                    _logger.Debug("等待后台初始化任务完成...");
                    try
                    {
                        await Task.WhenAny(_backgroundInitializationTask, Task.Delay(TimeSpan.FromSeconds(3)));
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "等待后台初始化任务失败");
                    }
                }

                //停止通讯
                _logger.Debug("停止通讯服务");
                _communicationService?.Stop();
                await Task.Delay(300);

                // 关闭VPP
                _logger.Debug("关闭 Vision 服务");
                if (_visionService is IDisposable visionDisposable)
                {
                    visionDisposable.Dispose();
                }
                await Task.Delay(300);

                //保存相机配置
                _logger.Debug("保存相机配置");
                string defaultConfigPath = _cameraConfigService.GetDefaultConfigPath();
                if (_cameraConfigService?.CurrentCogAcqFifoTool?.Operator != null)
                {
                    await _cameraConfigService.SaveConfigAsync(defaultConfigPath);
                    _logger.Information("相机配置已保存");
                }

                //释放相机资源
                _logger.Debug("释放相机资源");
                if (_cameraConfigService is IDisposable cameraDisposable)
                {
                    cameraDisposable.Dispose();
                }
                await Task.Delay(500);

                _logger.Information("========== 应用关闭流程完成 ==========");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "应用关闭过程中出错");
            }
        }
    }
}
