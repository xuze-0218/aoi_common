using aoi_common.Events;
using Cognex.VisionPro;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.ViewModels
{
    public class AlgorithmDebugViewModel : BindableBase, IDialogAware
    {
        private readonly ILogger _logger;
        public string Title => "程序调试窗口";
        public event Action<IDialogResult> RequestClose;
        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { _logger.Information("ToolBlock调试窗口已关闭"); }
        public void OnDialogOpened(IDialogParameters parameters) {}
        public AlgorithmDebugViewModel(ILogger logger)
        {
            _logger = logger;
            _logger.Information("ToolBlock调试窗口已打开");
        }
    }
}
