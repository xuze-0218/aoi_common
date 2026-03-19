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
        private readonly ICameraConfigService _cameraConfigService;
        private readonly IVisionService _visionService;
        private readonly ILogger _logger;

        public ApplicationStartupService(
            ICameraConfigService cameraConfigService,
            IVisionService visionService,
            ILogger logger)
        {
            _cameraConfigService = cameraConfigService;
            _visionService = visionService;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            _logger.Information("开始应用初始化...");

            try
            {
                // 第一步：加载相机配置
                await InitializeCameraAsync();

                // 第二步：初始化VisionService
                await InitializeVisionServiceAsync();

                _logger.Information("��用初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "应用初始化失败");
                throw;
            }
        }

        private async Task InitializeCameraAsync()
        {
            _logger.Information("开始初始化相机配置...");

            try
            {
                // 获取或创建CogAcqFifoTool
                _cameraConfigService.GetOrCreateAcqFifoTool();

                // 尝试加载本地配置
                string defaultConfigPath = _cameraConfigService.GetDefaultConfigPath();
                bool configLoaded = await _cameraConfigService.LoadConfigAsync(defaultConfigPath);

                if (configLoaded)
                {
                    _logger.Information("相机配置已从本地文件加载");
                }
                else
                {
                    _logger.Information("使用默认相机配置（用户可在调试界面修改）");
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
                // 这里填写你的vpp文件路径
                string vppPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Resources",
                    "toolblock.vpp");

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

        public async Task ShutdownAsync()
        {
            _logger.Information("应用关闭，保存相机配置...");

            try
            {
                // 保存当前的相机配置
                string defaultConfigPath = _cameraConfigService.GetDefaultConfigPath();
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
