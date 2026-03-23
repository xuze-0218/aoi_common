using aoi_common.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace aoi_common.Views
{
    /// <summary>
    /// CameraDebugView.xaml 的交互逻辑
    /// </summary>
    public partial class CameraDebugView : UserControl
    {
        public CameraDebugView(ICameraConfigService cameraService)
        {
            InitializeComponent();
            if (cameraService.CurrentCogAcqFifoTool != null)
            {
                cogAcqFifoEditV2.Subject = cameraService.CurrentCogAcqFifoTool;
            }
            this.Unloaded += (s, e) => { cogAcqFifoEditV2.Subject = null; };
        }
    }
}
