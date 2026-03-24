using aoi_common.Common;
using aoi_common.Services;
using aoi_common.ViewModels;
using aoi_common.Views;
using Prism.DryIoc;
using Prism.Ioc;
using Serilog;
using System;
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
            //相机配置
            containerRegistry.RegisterSingleton<ICameraConfigService, CameraConfigService>();
            //通讯服务
            containerRegistry.RegisterSingleton<ICommunicationService, CommunicationService>();
            //报文解析
            containerRegistry.RegisterSingleton<IDetectionLogicService, DetectionLogicService>();
            //参数配置
            containerRegistry.RegisterSingleton<IParametersConfigService, ParametersConfigService>();
            // 注册Vision服务
            containerRegistry.RegisterSingleton<IVisionService, VisionService>();
            // 注册应用启动服务
            containerRegistry.RegisterSingleton<IApplicationStartupService, ApplicationStartupService>();
            //相机调试
            containerRegistry.RegisterDialog<CameraDebugView, CameraDebugViewModel>(); 
            //程序调试
            containerRegistry.RegisterDialog<AlgorithmDebugView, AlgorithmDebugViewModel>();
            //参数配置逻辑
            containerRegistry.RegisterDialog<ParamConfigView, ParamConfigViewModel>();
            //通讯配置逻辑
            containerRegistry.RegisterDialog<CommunicationView, CommunicationViewModel>();

            Log.Logger = new LoggerConfiguration().MinimumLevel.Information().Enrich.FromLogContext()
                .WriteTo.Async(a => a.File("Logs/log_.txt",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 30)).WriteTo.Sink(new UiLogSink()).CreateLogger();

            containerRegistry.RegisterInstance<ILogger>(Log.Logger);
        }

        protected override async void OnInitialized()
        {
            base.OnInitialized();
            //优化WinForm控件的视觉样式
            System.Windows.Forms.Application.EnableVisualStyles();
            try
            {
                var startupService = Container.Resolve<IApplicationStartupService>();
                await startupService.InitializeAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "应用启动失败");
                MessageBox.Show("应用初始化失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }          
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            try
            {
                var startupService = Container.Resolve<IApplicationStartupService>();
                startupService.ShutdownAsync().Wait();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "应用关闭时保存配置失败");
            }

            Log.CloseAndFlush();
        }
    }
}
