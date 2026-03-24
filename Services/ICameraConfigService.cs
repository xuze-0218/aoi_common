using aoi_common.Models;
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
        /// 获取当前的 ICogAcqFifo 接口（用于直接图像采集）
        /// </summary>
        ICogAcqFifo CurrentCogAcqFifo { get; }
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
        bool IsReady();

        /// <summary>
        /// 启动采集  非阻塞
        /// PLC触发或用户点击
        /// </summary>
        void StartCapture();
        event Action<ICogImage> OnImageCaptured;

    }

    public class CameraConfigService : ICameraConfigService
    {
        private CogAcqFifoTool _cogAcqFifoTool;
        private readonly ILogger _logger;
        private readonly string _configFolder;
        private const string DEFAULT_CONFIG_FILENAME = "CameraConfig.vpp";

        public CogAcqFifoTool CurrentCogAcqFifoTool => _cogAcqFifoTool;
        public ICogAcqFifo CurrentCogAcqFifo => _cogAcqFifoTool?.Operator as ICogAcqFifo;
        public event Action<ICogImage> OnImageCaptured;
        public CameraConfigService(ILogger logger)
        {
            _logger = logger;
            _configFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            EnsureConfigFolderExists();
        }

        private void CurrentCogAcqFifo_Complete(object sender, CogCompleteEventArgs e)
        {
            try
            {
                int numPending, numReady;
                bool isBusy;
                CurrentCogAcqFifo.GetFifoState(out numPending, out numReady, out isBusy);

                if (numReady > 0)
                {
                    CogAcqInfo info = new CogAcqInfo();
                    ICogImage image = CurrentCogAcqFifo.CompleteAcquireEx(info);

                    if (image != null)
                    {
                        _logger.Information("【采集完成】已获取图像，触发事件");
                        OnImageCaptured?.Invoke(image);
                    }
                    else
                    {
                        _logger.Warning("【采集完成】图像为空");
                    }
                }
                else
                {
                    _logger.Warning("【采集完成】FIFO中无数据");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "【采集完成事件】异常");
            }
        }

        private void EnsureConfigFolderExists()
        {
            if (!Directory.Exists(_configFolder))
            {
                Directory.CreateDirectory(_configFolder);
                _logger.Debug("创建相机配置文件夹: {ConfigFolder}", _configFolder);
            }
        }

        private void BindAcqFifoEvent()
        {
            var fifo = CurrentCogAcqFifo;
            if (fifo != null)
            {
                fifo.Complete -= CurrentCogAcqFifo_Complete; 
                fifo.Complete += CurrentCogAcqFifo_Complete;
            }
        }

        public CogAcqFifoTool GetOrCreateAcqFifoTool()
        {
            if (_cogAcqFifoTool != null)
                return _cogAcqFifoTool;

            try
            {
                _cogAcqFifoTool = new CogAcqFifoTool();
                BindAcqFifoEvent();
                _logger.Debug("创建新的CogAcqFifoTool实例");
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
                    BindAcqFifoEvent();
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

        public bool IsReady()
        {
            if (_cogAcqFifoTool == null || _cogAcqFifoTool.Operator == null)
                return false;

            try
            {
                return _cogAcqFifoTool.Operator != null;
            }
            catch
            {
                return false;
            }
        }


        public string GetDefaultConfigPath()
        {
            return Path.Combine(_configFolder, DEFAULT_CONFIG_FILENAME);
        }

        ///启动采集  非阻塞、立即返回
        public void StartCapture()
        {
            if (!IsReady())
            {
                _logger.Error("【相机采集】启动失败：相机未准备就绪");
                return;
            }

            try
            {
                ICogAcqFifo cogAcqFifo = CurrentCogAcqFifo;
                if (cogAcqFifo == null)
                {
                    _logger.Error("【相机采集】启动失败：无法获取 ICogAcqFifo");
                    return;
                }

                _logger.Information("【相机采集】已启动（非阻塞）");
                cogAcqFifo.StartAcquire();
                _logger.Debug("【相机采集】相机后台正在拍照...");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "【相机采集】启动异常");
            }
        }

    }
}
