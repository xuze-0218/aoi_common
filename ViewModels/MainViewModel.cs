using aoi_common.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
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

        public DelegateCommand OpenDebugCommand { get; }
        public DelegateCommand ParaDebugCommand { get; }
        public DelegateCommand CommunicateDebugCommand { get; }
        public DelegateCommand ImportImageCommand { get; }
        public DelegateCommand RunCommand { get; }

        public MainViewModel(IVisionService visionService, IDialogService dialogService)
        {
            _visionService = visionService;
            _dialogService = dialogService;
            OpenDebugCommand = new DelegateCommand(() => { _dialogService.Show("DebugView", new DialogParameters(), r => { }); });
            ParaDebugCommand = new DelegateCommand(() => { _dialogService.Show("ParamConfigView", new DialogParameters(), r => { }); });
            CommunicateDebugCommand = new DelegateCommand(() => { _dialogService.Show("CommunicationView", new DialogParameters(), r => { }); });

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
        }
    }
}
