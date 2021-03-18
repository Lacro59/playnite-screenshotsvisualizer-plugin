using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.IO;
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

namespace ScreenshotsVisualizer.Views
{
    /// <summary>
    /// Logique d'interaction pour SsvSinglePictureView.xaml
    /// </summary>
    public partial class SsvSinglePictureView : UserControl
    {
        public SsvSinglePictureView(Screenshot screenshot)
        {
            InitializeComponent();

            string PictureSource = string.Empty;
            if (File.Exists(screenshot.FileName))
            {
                PictureSource = screenshot.FileName;
            }

            this.DataContext = new
            {
                PictureSource
            };
        }


        private void PART_Contener_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            PART_ScreenshotsPicture.Height = PART_Contener.ActualHeight;
            PART_ScreenshotsPicture.Width = PART_Contener.ActualWidth;
        }
    }
}
