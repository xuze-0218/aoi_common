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
        private readonly IVisionService _visionService;
        private readonly IDialogService _dialogService;
        public ObservableCollection<LogEventModel> LogSource => UiLogSink.LogCollection;
        public DelegateCommand OpenDebugCommand { get; }
        public DelegateCommand ParaDebugCommand { get; }
        public DelegateCommand CommunicateDebugCommand { get; }
        public DelegateCommand ImportImageCommand { get; }
        public DelegateCommand RunCommand { get; }

        public MainViewModel(IVisionService visionService, IDialogService dialogService)
        {
            _visionService = visionService;
            _dialogService = dialogService;
            OpenDebugCommand = new DelegateCommand(() =>
            {
                if (!_visionService.IsInitialized)
                {
                   
                    return;
                }
                _dialogService.Show("DebugView",
                    new DialogParameters(), r => { });
            }, () => _visionService.IsInitialized);
            ParaDebugCommand = new DelegateCommand
                (() => { _dialogService.Show("ParamConfigView", new DialogParameters(), r => { }); });

            CommunicateDebugCommand = new DelegateCommand
                (() => { _dialogService.Show("CommunicationView", new DialogParameters(), r => { }); });

            ImportImageCommand = new DelegateCommand(() =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "图片文件|*.bmp;*.jpg;*.png;*.idb" };
                if (ofd.ShowDialog() == true)
                {
                    _visionService.ChangeImagePath(ofd.FileName);
                    _visionService.RunTool();
                }
            });

            RunCommand = new DelegateCommand(() =>
            {

                _visionService.RunTool();
            });
            MonitorStatus();
            Log.Information("AOI 系统主界面加载完成。");
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
