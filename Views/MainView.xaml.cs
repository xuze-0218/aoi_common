using aoi_common.Events;
using Prism.Events;
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
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainView
    {
        public MainView(IEventAggregator eventAggregator)
        {
            InitializeComponent();
            eventAggregator.GetEvent<ICogImageDisplayEvent>().Subscribe(record =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (record != null)
                    {
                        mainRecordDisplay.Record = record;
                        mainRecordDisplay.Fit(true);
                    }
                });
            });
        }
    }
}
