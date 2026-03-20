using aoi_common.Services;
using aoi_common.ViewModels;
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
    /// RunView.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmDebugView : UserControl
    {
        public AlgorithmDebugView(IVisionService visionService)
        {
            InitializeComponent();
            if (visionService.toolBlock!=null)
            {
                toolBlockEditV2.Subject = visionService.toolBlock;
            }
            this.Unloaded += (s, e) => { toolBlockEditV2.Subject = null; };
        }
    }
}
