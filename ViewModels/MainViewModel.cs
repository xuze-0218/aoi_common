using aoi_common.Common;
using aoi_common.Models;
using aoi_common.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace aoi_common.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private readonly ICameraConfigService _cameraService;
        private readonly IVisionService _visionService;
        private readonly IDialogService _dialogService;
        private readonly ILogger _logger;

        /// <summary>
        /// 当前运行模式
        /// </summary>
        private string _modeIndicator = "离线模式";
        public string ModeIndicator
        {
            get => _modeIndicator;
            set => SetProperty(ref _modeIndicator, value);
        }

        /// <summary>
        /// 按钮启用状态
        /// </summary>
        private bool _isOnlineRunning;
        public bool IsOnlineRunning
        {
            get => _isOnlineRunning;
            set
            {
                if (SetProperty(ref _isOnlineRunning, value))
                {
                    OnlineStartCommand?.RaiseCanExecuteChanged();
                    OnlineStopCommand?.RaiseCanExecuteChanged();
                    SnapCommand?.RaiseCanExecuteChanged();
                }
            }
        }



        public ObservableCollection<LogEventModel> LogSource => UiLogSink.LogCollection;
        public DelegateCommand OpenDebugCommand { get; private set; }
        public DelegateCommand CameraDebugCommand { get; private set; }
        public DelegateCommand ParaDebugCommand { get; private set; }
        public DelegateCommand CommunicateDebugCommand { get; private set; }
        public DelegateCommand ImportImageCommand { get; private set; }

        public DelegateCommand OfflineSingleImageCommand { get; private set; }      // 离线单张
        public DelegateCommand OfflineFolderBatchCommand { get; private set; }      // 离线文件夹
        public DelegateCommand OnlineStartCommand { get; private set; }             // 在线测试
        public DelegateCommand OnlineStopCommand { get; private set; }              // 在线停止
        public DelegateCommand SnapCommand { get; private set; }                    // 拍照测试


        public MainViewModel(IVisionService visionService, IDialogService dialogService, ICameraConfigService cameraService, ILogger logger)
        {

            _cameraService = cameraService;
            _visionService = visionService;
            _dialogService = dialogService;
            _logger = logger;
            InitializeCommands();
            MonitorStatus();
            _logger.Information("AOI系统界面加载完成");

        }

        private void InitializeCommands()
        {
            OpenDebugCommand = new DelegateCommand(
                () => { _dialogService.Show("AlgorithmDebugView", new DialogParameters(), r => { }); },
                () => _visionService.IsInitialized);

            CameraDebugCommand = new DelegateCommand(
                () => { _dialogService.Show("CameraDebugView", new DialogParameters(), r => { }); }
                /*, () => _cameraService.IsReady*/);

            ParaDebugCommand = new DelegateCommand(
                () => { _dialogService.Show("ParamConfigView", new DialogParameters(), r => { }); });

            CommunicateDebugCommand = new DelegateCommand(
                () => { _dialogService.Show("CommunicationView", new DialogParameters(), r => { }); });

            OfflineSingleImageCommand = new DelegateCommand(() =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图片文件|*.bmp;*.jpg;*.png;*.tiff|All Files|*.*"
                };

                if (ofd.ShowDialog() == true)
                {
                    try
                    {
                        _logger.Information("开始单张图像测试: {FilePath}", ofd.FileName);
                        ModeIndicator = "离线模式 - 单张";
                        IsOnlineRunning = false;
                        LocalFileImageSource imageSource = new LocalFileImageSource(ofd.FileName, _logger);
                        _visionService.RunToolWithImageSource(imageSource);
                        imageSource.Dispose();
                        _logger.Debug("单张图像测试完成");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "单张图像测试失败");
                    }
                }
            });

            OfflineFolderBatchCommand = new DelegateCommand(() =>
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择包含图像的文件夹"
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        ModeIndicator = "离线模式 - 批量";
                        IsOnlineRunning = false;
                        _logger.Information("开始批量测试: {FolderPath}", dialog.SelectedPath);
                        LocalFolderImageSource imageSource = new LocalFolderImageSource(dialog.SelectedPath, _logger);
                        if (imageSource.TotalCount == 0)
                        {
                            _logger.Warning("文件夹中没有支持的图像文件");
                            return;
                        }

                        _logger.Debug("共找到{Count}张图像,开始处理...", imageSource.TotalCount);
                        _visionService.RunToolWithImageSource(imageSource);
                        imageSource.Dispose();

                        _logger.Debug("批量测试完成");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "批量测试失败");
                    }
                }
            });

            OnlineStartCommand = new DelegateCommand(() =>
            {
                try
                {
                    _logger.Information("点击启动按钮");
                    IsOnlineRunning = true;
                    ModeIndicator = "在线模式 - 等待 PLC 信号或手动拍照";
                    //_visionService.RunToolOnline();
                    _logger.Information("在线模式已启动，等待PLC信号或测试拍照");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "在线模式启动失败");
                    IsOnlineRunning = false;
                    ModeIndicator = "离线模式";
                }
            },
            () => _visionService.IsInitialized && !IsOnlineRunning);

            OnlineStopCommand = new DelegateCommand(() =>
            {
                try
                {
                    _logger.Debug("在线模式停止");
                    IsOnlineRunning = false;
                    ModeIndicator = "离线模式";
                    _logger.Information("已退出在线模式");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "在线模式停止失败");
                }
            }, () => IsOnlineRunning);

            SnapCommand = new DelegateCommand(() =>
            {
                try
                {
                    _logger.Debug("【快速拍照】手动触发");

                    if (!_cameraService.IsReady())
                    {
                        _logger.Error("相机未就绪");
                        return;
                    }
                    _visionService.RunToolOnline();

                    _logger.Information("拍照测试完成");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "拍照测试失败");
                }
            },
       () => IsOnlineRunning && _cameraService.IsReady());

        }

        private async void MonitorStatus()
        {
            while (!_visionService.IsInitialized)
            {
                await Task.Delay(500);
            }
            OpenDebugCommand.RaiseCanExecuteChanged();
            OnlineStartCommand.RaiseCanExecuteChanged();
            OnlineStopCommand.RaiseCanExecuteChanged();
            SnapCommand.RaiseCanExecuteChanged();
            //ParaDebugCommand.RaiseCanExecuteChanged();
            Log.Information("Vpp文件加载完成，可打开调试窗口查看");
        }
    }
}
