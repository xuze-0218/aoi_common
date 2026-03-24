using aoi_common.Services;
using Cognex.VisionPro;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace aoi_common.ViewModels
{
    public class CameraDebugViewModel : BindableBase, IDialogAware
    {

        private readonly ICameraConfigService _cameraConfigService;
        private readonly ILogger _logger;

        private CogAcqFifoTool _currentCogAcqFifoTool;
        private string _statusMessage = "就绪";
        private bool _isConfigLoaded = false;

        public event Action<IDialogResult> RequestClose;

        public CogAcqFifoTool CurrentCogAcqFifoTool
        {
            get { return _currentCogAcqFifoTool; }
            set { SetProperty(ref _currentCogAcqFifoTool, value); }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }

        public bool IsConfigLoaded
        {
            get { return _isConfigLoaded; }
            set { SetProperty(ref _isConfigLoaded, value); }
        }

        public ICommand LoadConfigCommand { get; private set; }
        public ICommand SaveConfigCommand { get; private set; }
        public ICommand ReloadDefaultCommand { get; private set; }

        public string Title => "相机调试窗口";

        public CameraDebugViewModel(ICameraConfigService cameraConfigService, ILogger logger)
        {
            _cameraConfigService = cameraConfigService;
            _logger = logger;

            InitializeCommands();
            InitializeCamera();
        }

        private void InitializeCommands()
        {
            LoadConfigCommand = new DelegateCommand(LoadConfig);
            SaveConfigCommand = new DelegateCommand(SaveConfig);
            ReloadDefaultCommand = new DelegateCommand(ReloadDefault);
        }

        /// <summary>
        /// 初始化相机 - 获取当前的CogAcqFifoTool
        /// </summary>
        private void InitializeCamera()
        {
            try
            {
                CurrentCogAcqFifoTool = _cameraConfigService.CurrentCogAcqFifoTool;

                if (CurrentCogAcqFifoTool != null)
                {
                  
                    IsConfigLoaded = _cameraConfigService.IsReady();
                    StatusMessage = "相机已初始化";
                    _logger.Information("相机调试界面已初始化");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "初始化失败: " + ex.Message;
                _logger.Error(ex, "相机调试界面初始化失败");
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfig()
        {
            // 打开文件对话框
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "VisionPro Config Files (*.vpp)|*.vpp|All Files (*.*)|*.*";
            dialog.DefaultExt = ".vpp";
            dialog.InitialDirectory = _cameraConfigService.GetDefaultConfigPath();

            if (dialog.ShowDialog() == true)
            {
                LoadConfigAsync(dialog.FileName);
            }
        }

        /// <summary>
        /// 异步加载配置
        /// </summary>
        private async void LoadConfigAsync(string configPath)
        {
            StatusMessage = "正在加载配置...";

            try
            {
                bool result = await _cameraConfigService.LoadConfigAsync(configPath);

                if (result)
                {
                    CurrentCogAcqFifoTool = _cameraConfigService.CurrentCogAcqFifoTool;
                    IsConfigLoaded = true;
                    StatusMessage = "配置加载成功";
                    _logger.Information("相机配置已加载: {ConfigPath}", configPath);
                }
                else
                {
                    StatusMessage = "配置加载失败";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "加载出错: " + ex.Message;
                _logger.Error(ex, "加载相机配置失败");
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfig()
        {
            SaveConfigAsync(null);
        }

        /// <summary>
        /// 异步保存配置
        /// </summary>
        private async void SaveConfigAsync(string configPath)
        {
            StatusMessage = "正在保存配置...";

            try
            {
                bool result = await _cameraConfigService.SaveConfigAsync(configPath);

                if (result)
                {
                    IsConfigLoaded = true;
                    StatusMessage = "配置保存成功";
                    _logger.Information("相机配置已保存");
                }
                else
                {
                    StatusMessage = "配置保存失败";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "保存出错: " + ex.Message;
                _logger.Error(ex, "保存相机配置失败");
            }
        }

        /// <summary>
        /// 重新加载默认配置
        /// </summary>
        private void ReloadDefault()
        {
            string defaultPath = _cameraConfigService.GetDefaultConfigPath();
            LoadConfigAsync(defaultPath);
        }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() => _logger.Information("ToolBlock调试窗口已关闭");

        public void OnDialogOpened(IDialogParameters parameters) { }
    }
}
