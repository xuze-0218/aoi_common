using aoi_common.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace aoi_common.Common
{
    /// <summary>
    /// 根据长度类型改变文本颜色
    /// Dynamic → 蓝色（提醒用户这是动态的）
    /// Fixed → 黑色
    /// </summary>
    public class LengthTypeForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LengthType lengthType)
            {
                return lengthType == LengthType.Dynamic
                    ? new SolidColorBrush(Colors.DodgerBlue)
                    : new SolidColorBrush(Colors.Black);
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
