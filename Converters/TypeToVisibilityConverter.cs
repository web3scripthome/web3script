using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace web3script.Converters
{
    /// <summary>
    /// 将对象类型转换为可见性的转换器
    /// 如果对象是指定类型则显示，否则隐藏
    /// </summary>
    public class TypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;
                
            string targetTypeName = parameter.ToString();
            string actualTypeName = value.GetType().Name;
            
            return actualTypeName == targetTypeName ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    /// <summary>
    /// 将对象类型转换为反向可见性的转换器
    /// 如果对象不是指定类型则显示，否则隐藏
    /// </summary>
    public class TypeToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Visible;
                
            string targetTypeName = parameter.ToString();
            string actualTypeName = value.GetType().Name;
            
            return actualTypeName != targetTypeName ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 