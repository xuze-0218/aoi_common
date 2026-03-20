using Cognex.VisionPro;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Services
{
    public interface ICameraConfigService
    {
        /// <summary>
        /// 获取当前的实例
        /// </summary>
        CogAcqFifoTool CurrentCogAcqFifoTool { get; }
        /// <summary>
        /// 获取或创建CogAcqFifoTool实例
        /// </summary>
        CogAcqFifoTool GetOrCreateAcqFifoTool();
        /// <summary>
        /// 从文件加载相机配置
        /// </summary>
        Task<bool> LoadConfigAsync(string configPath);
        Task<bool> SaveConfigAsync(string configPath);
        string GetDefaultConfigPath();
        bool IsInitialized { get; }
    }

    public class CameraConfigService : ICameraConfigService
    {
        private CogAcqFifoTool _cogAcqFifoTool;
        private readonly ILogger _logger;
        private readonly string _configFolder;
        private const string DEFAULT_CONFIG_FILENAME = "CameraConfig.vpp";

        public CogAcqFifoTool CurrentCogAcqFifoTool => _cogAcqFifoTool;

        public bool IsInitialized => _cogAcqFifoTool != null;
        
        public CameraConfigService(ILogger logger)
        {
            _logger = logger;
            _configFolder= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            EnsureConfigFolderExists();
        }

        private void EnsureConfigFolderExists()
        {
            if (!Directory.Exists(_configFolder))
            {
                Directory.CreateDirectory(_configFolder);
                _logger.Information("创建相机配置文件夹: {ConfigFolder}", _configFolder);
            }
        }

        public CogAcqFifoTool GetOrCreateAcqFifoTool()
        {
            if (_cogAcqFifoTool != null)
                return _cogAcqFifoTool;

            try
            {
                _cogAcqFifoTool = new CogAcqFifoTool();
                _logger.Information("创建新的CogAcqFifoTool实例");
                return _cogAcqFifoTool;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "创建CogAcqFifoTool失败");
                throw;
            }
        }

        public async Task<bool> LoadConfigAsync(string configPath)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(configPath))
                    configPath = GetDefaultConfigPath();

                if (!File.Exists(configPath))
                {
                    _logger.Warning("相机配置文件不存在: {ConfigPath}", configPath);
                    return false;
                }

                try
                {
                    _cogAcqFifoTool = (CogAcqFifoTool)CogSerializer.LoadObjectFromFile(configPath);
                    _logger.Information("成功加载相机配置: {ConfigPath}", configPath);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "加载相机配置失败: {ConfigPath}", configPath);
                    return false;
                }
            });
        }

        public async Task<bool> SaveConfigAsync(string configPath)
        {
            return await Task.Run(() =>
            {
                if (_cogAcqFifoTool == null)
                {
                    _logger.Warning("CogAcqFifoTool未初始化，无法保存配置");
                    return false;
                }

                if (string.IsNullOrEmpty(configPath))
                    configPath = GetDefaultConfigPath();

                try
                {
                    string directory = Path.GetDirectoryName(configPath);
                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    CogSerializer.SaveObjectToFile(_cogAcqFifoTool, configPath);
                    _logger.Information("成功保存相机配置: {ConfigPath}", configPath);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "保存相机配置失败: {ConfigPath}", configPath);
                    return false;
                }
            });
        }

        public string GetDefaultConfigPath()
        {
            return Path.Combine(_configFolder, DEFAULT_CONFIG_FILENAME);
        }
    }
}
