using aoi_common.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.ViewModels
{
    public class MainViewModel:BindableBase
    {
        private readonly IRegionManager _regionManager;
        private readonly IVisionService _visionService;
        public DelegateCommand<string> SwitchRunViewCommand { get; set; }
        public DelegateCommand RunVisionCommand { get; set; }


        public MainViewModel(IRegionManager regionManager, IVisionService visionService)
        {
            _regionManager = regionManager;
            _visionService = visionService;
            //SwitchRunViewCommand = new DelegateCommand<string>(path => _regionManager.RequestNavigate("MainRegion", path));
            SwitchRunViewCommand = new DelegateCommand<string>(path => _regionManager.RequestNavigate("MainRegion", "RunView", arg =>
            {
                if (arg.Result == false)
                {
                    System.Diagnostics.Debug.WriteLine($"导航失败原因: {arg.Error?.Message}");
                }
            }));
            RunVisionCommand = new DelegateCommand(() => { _visionService.RunTool(); });
        }
    }
}
