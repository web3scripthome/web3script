using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace web3script.Converters
{
    /// <summary>
    /// 将布尔值反转后转换为可见性的转换器
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // 反转布尔值并转换为可见性
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                // 从可见性反转回布尔值
                return visibility != Visibility.Visible;
            }
            
            return true;
        }
    }
} 
