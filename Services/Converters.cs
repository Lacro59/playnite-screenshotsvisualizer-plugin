using PluginCommon;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace ScreenshotsVisualizer.Services
{
    public class ImageConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string && !((string)value).IsNullOrEmpty() && File.Exists((string)value))
            {
                BitmapLoadProperties bitmapLoadProperties = null;
                if (parameter is string && (string)parameter == "1")
                {
                    bitmapLoadProperties = new BitmapLoadProperties(100, 0)
                    {
                        Source = (string)value
                    };
                }
                if (parameter is string && (string)parameter == "2")
                {
                    bitmapLoadProperties = new BitmapLoadProperties(200, 0)
                    {
                        Source = (string)value
                    };
                }


                if (((string)value).EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    BitmapImage bitmapImage = BitmapExtensions.TgaToBitmap((string)value);

                    if (bitmapLoadProperties == null)
                    {
                        return bitmapImage;
                    }
                    else
                    {
                        return bitmapImage.GetClone(bitmapLoadProperties);
                    }
                }
                

                if (bitmapLoadProperties == null)
                {
                    return BitmapExtensions.BitmapFromFile((string)value);
                }
                else
                {
                    return BitmapExtensions.BitmapFromFile((string)value, bitmapLoadProperties);
                }
            }
            
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
