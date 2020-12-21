using Pfim;
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
using ImageFormat = Pfim.ImageFormat;

namespace ScreenshotsVisualizer.Services
{
    public class TgaConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string && File.Exists((string)value))
            {
                if (((string)value).ToLower().Contains("tga"))
                {
                    using (var image = Pfim.Pfim.FromFile((string)value))
                    {
                        PixelFormat format;

                        // Convert from Pfim's backend agnostic image format into GDI+'s image format
                        switch (image.Format)
                        {
                            case ImageFormat.Rgba32:
                                format = PixelFormat.Format32bppArgb;
                                break;
                            case ImageFormat.Rgb24:
                                format = PixelFormat.Format24bppRgb;
                                break;
                            default:
                                // see the sample for more details
                                throw new NotImplementedException();
                        }

                        // Pin pfim's data array so that it doesn't get reaped by GC, unnecessary
                        // in this snippet but useful technique if the data was going to be used in
                        // control like a picture box
                        var handle = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
                        Bitmap bitmap = null;
                        try
                        {
                            var data = Marshal.UnsafeAddrOfPinnedArrayElement(image.Data, 0);
                            bitmap = new Bitmap(image.Width, image.Height, image.Stride, format, data);
                        }
                        finally
                        {
                            handle.Free();
                        }

                        if (bitmap != null)
                        {
                            return ImageTools.ConvertBitmapToBitmapImage(bitmap);
                        }
                    }
                }
                else if (!((string)value).IsNullOrEmpty())
                {
                    return new BitmapImage(new Uri((string)value));
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
