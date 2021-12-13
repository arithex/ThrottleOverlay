using System;
using System.Configuration;
using System.Windows;
using System.Globalization;

namespace ThrottleOverlay
{
    public partial class MainWindow : Window
    {
        public MainWindow( )
        {
            InitializeComponent();

            string xPos = ConfigurationManager.AppSettings["XPos"];
            string yPos = ConfigurationManager.AppSettings["YPos"];
            string width = ConfigurationManager.AppSettings["Width"];
            string height = ConfigurationManager.AppSettings["Height"];

            int x = Int32.Parse(xPos, CultureInfo.InvariantCulture);
            int y = Int32.Parse(yPos, CultureInfo.InvariantCulture);
            uint w = UInt32.Parse(width, CultureInfo.InvariantCulture);
            uint h = UInt32.Parse(height, CultureInfo.InvariantCulture);

            if (x < 0)
                x += (int)(SystemParameters.PrimaryScreenWidth - w);
            if (y < 0)
                y += (int)(SystemParameters.PrimaryScreenHeight - h);

            this.Left = x;
            this.Top = y;
            this.Width = UInt32.Parse(width, CultureInfo.InvariantCulture);
            this.Height = UInt32.Parse(height, CultureInfo.InvariantCulture);
        }
    }
}
