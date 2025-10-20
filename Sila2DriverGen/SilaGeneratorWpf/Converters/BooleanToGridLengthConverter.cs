using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SilaGeneratorWpf.Converters
{
    /// <summary>
    /// 将布尔值转换为GridLength，用于控制侧边栏的显示/隐藏
    /// </summary>
    public class BooleanToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible)
            {
                return isVisible ? new GridLength(350) : new GridLength(0);
            }
            return new GridLength(350);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

