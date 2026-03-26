using aoi_common.Models;
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
    /// ProtocolConfigView.xaml 的交互逻辑
    /// </summary>
    public partial class ProtocolConfigView : UserControl
    {
        public ProtocolConfigView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 删除输入字段
        /// </summary>
        private void OnDeleteInputField(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var field = button?.DataContext as ProtocolField;

                if (field != null && this.DataContext is ProtocolConfigViewModel viewModel)
                {
                    viewModel.InputFields.Remove(field);
                    viewModel.StatusMessage = "已删除输入字段";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除输出字段
        /// </summary>
        private void OnDeleteOutputField(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var field = button?.DataContext as ProtocolField;

                if (field != null && this.DataContext is ProtocolConfigViewModel viewModel)
                {
                    viewModel.OutputFields.Remove(field);
                    viewModel.StatusMessage = "已删除输出字段";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
