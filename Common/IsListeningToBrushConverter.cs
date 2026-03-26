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
    public class IsListeningToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isListening)
            {
                return isListening
                    ? new SolidColorBrush(Color.FromArgb(255, 144, 238, 144))  // LightGreen
                    : new SolidColorBrush(Color.FromArgb(255, 211, 211, 211));  // LightGray
            }
            return new SolidColorBrush(Colors.LightGray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
