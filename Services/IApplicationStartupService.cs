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

        public ApplicationStartupService(ICameraConfigService cameraConfigService, IVisionService visionService, ILogger logger)
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
                await InitializeCameraAsync();
                await InitializeVisionServiceAsync();
                _logger.Information("应用初始化完成");
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
