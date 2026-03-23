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
        public ObservableCollection<LogEventModel> LogSource => UiLogSink.LogCollection;
        public DelegateCommand OpenDebugCommand { get; private set; }
        public DelegateCommand CameraDebugCommand { get; private set; }
        public DelegateCommand ParaDebugCommand { get; private set; }
        public DelegateCommand CommunicateDebugCommand { get; private set; }
        public DelegateCommand ImportImageCommand { get; private set; }
        public ICommand RunSingleImageCommand { get; private set; }
        public ICommand RunFolderBatchCommand { get; private set; }
        public DelegateCommand RunCommand { get; private set; }

        public MainViewModel(IVisionService visionService, IDialogService dialogService,ICameraConfigService cameraService, ILogger logger)
        {
            
            _cameraService = cameraService;
            _visionService = visionService;
            _dialogService = dialogService;
            _logger = logger;
            InitializeCommands();
            MonitorStatus();
            _logger.Information("AOI 系统主界面加载完成。");

        }

        private void InitializeCommands()
        {
            OpenDebugCommand = new DelegateCommand(
                () => { _dialogService.Show("AlgorithmDebugView", new DialogParameters(), r => { }); },
                () => _visionService.IsInitialized);

            CameraDebugCommand = new DelegateCommand(
                () => { _dialogService.Show("CameraDebugView", new DialogParameters(), r => { }); },
                () => _cameraService.IsInitialized);

            ParaDebugCommand = new DelegateCommand(
                () => { _dialogService.Show("ParamConfigView", new DialogParameters(), r => { }); });

            CommunicateDebugCommand = new DelegateCommand(
                () => { _dialogService.Show("CommunicationView", new DialogParameters(), r => { }); });

            RunSingleImageCommand = new DelegateCommand(() =>
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
                        LocalFileImageSource imageSource = new LocalFileImageSource(ofd.FileName, _logger);
                        _visionService.RunToolWithImageSource(imageSource);
                        imageSource.Dispose();
                        _logger.Information("单张图像测试完成");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "单张图像测试失败");
                    }
                }
            });

            RunFolderBatchCommand = new DelegateCommand(() =>
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择包含图像的文件夹"
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        _logger.Information("开始批量测试: {FolderPath}", dialog.SelectedPath);
                        LocalFolderImageSource imageSource = new LocalFolderImageSource(dialog.SelectedPath, _logger);

                        if (imageSource.TotalCount == 0)
                        {
                            _logger.Warning("文件夹中没有支持的图像文件");
                            return;
                        }

                        _logger.Information("共找到 {Count} 张图像，开始处理...", imageSource.TotalCount);
                        _visionService.RunToolWithImageSource(imageSource);
                        imageSource.Dispose();
                        _logger.Information("文件夹批量测试完成");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "文件夹批量测试失败");
                    }
                }
            });

            RunCommand = new DelegateCommand(() =>
            {
                try
                {
                    _visionService.RunToolOnline();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "单次测试失败");
                }
            });
        }

        private async void MonitorStatus()
        {
            while (!_visionService.IsInitialized)
            {
                await Task.Delay(500);
            }
            OpenDebugCommand.RaiseCanExecuteChanged();
            //ParaDebugCommand.RaiseCanExecuteChanged();
            Log.Information("Vpp初始化完成，可打开调试窗口查看");
        }
    }
}
