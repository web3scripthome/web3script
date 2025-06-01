using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace web3script.Converters
{
    /// <summary>
    /// 将集合计数转换为可见性的转换器
    /// 当计数为0时显示，否则隐藏
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 