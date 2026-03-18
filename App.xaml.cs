using aoi_common.Services;
using aoi_common.ViewModels;
using aoi_common.Views;
using Prism.DryIoc;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace aoi_common
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {
           return Container.Resolve<MainView>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<ICommunicationService,CommunicationService>();
            containerRegistry.RegisterSingleton<IVisionService, VisionService>();
            containerRegistry.RegisterDialog<DebugView, DebugViewModel>();
            containerRegistry.RegisterDialog<ParamConfigView,ParamConfigViewModel>();
            containerRegistry.RegisterDialog<CommunicationView,CommunicationViewModel>();
        }


        protected override void OnInitialized()
        {
            base.OnInitialized();
            //优化WinForm控件的视觉样式
            System.Windows.Forms.Application.EnableVisualStyles();
            var visionservice = Container.Resolve<IVisionService>();
            Task.Run(async () => await visionservice.InitialAsync("C:\\Users\\xuze\\Desktop\\testvpp.vpp"));
        }
    }
}
