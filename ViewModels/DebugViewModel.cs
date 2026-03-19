using aoi_common.Events;
using Cognex.VisionPro;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.ViewModels
{
    public class DebugViewModel : BindableBase, IDialogAware
    {
        public string Title => "调试窗口";

        public event Action<IDialogResult> RequestClose;
        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters parameters) { }
    }
}
