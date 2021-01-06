using CommonPluginsShared;
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
    public class ImageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is string && !((string)values[0]).IsNullOrEmpty() && File.Exists((string)values[0]))
            {
                BitmapLoadProperties bitmapLoadProperties = null;
                if (parameter is string && (string)parameter == "1")
                {
                    bitmapLoadProperties = new BitmapLoadProperties(100, 0)
                    {
                        Source = (string)values[0]
                    };
                }
                if (parameter is string && (string)parameter == "2")
                {
                    bitmapLoadProperties = new BitmapLoadProperties(200, 0)
                    {
                        Source = (string)values[0]
                    };
                }
                if (parameter is string && (string)parameter == "0")
                {
                    double ActualHeight = (double)values[1];

                    if (ActualHeight > 200)
                    {
                        bitmapLoadProperties = new BitmapLoadProperties((int)ActualHeight, 0)
                        {
                            Source = (string)values[0]
                        };
                    }
                    else
                    {
                        bitmapLoadProperties = new BitmapLoadProperties(200, 0)
                        {
                            Source = (string)values[0]
                        };
                    }                    
                }


                if (((string)values[0]).EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    BitmapImage bitmapImage = BitmapExtensions.TgaToBitmap((string)values[0]);

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
                    return BitmapExtensions.BitmapFromFile((string)values[0]);
                }
                else
                {
                    return BitmapExtensions.BitmapFromFile((string)values[0], bitmapLoadProperties);
                }
            }
            
            return values[0];
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
