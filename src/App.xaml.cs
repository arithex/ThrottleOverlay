using System;
using System.Windows;
using System.Windows.Media;

namespace ThrottleOverlay
{
    public partial class App : Application
    {
        static IntPtr s_msgWndForStick = IntPtr.Zero;
        static RawInputJoystickHandler s_stickInputHandler = null;

        //--------------------------------------------------------------
        // Initialization

        //----------------------------------------
        protected override void OnStartup( StartupEventArgs e )
        {
            base.OnStartup(e);

            // Create hidden HWND to subscribe to Raw Input (WM_INPUT) events.
            Win32.MessageWindow.InitWindowClass(_WndProc);

            s_msgWndForStick = Win32.MessageWindow.CreateMessageWindow();
            s_stickInputHandler = new RawInputJoystickHandler(s_msgWndForStick);

            return;
        }

        //--------------------------------------------------------------
        // Raw Input window message handler

        //----------------------------------------
        static int _WndProc( IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam )
        {
            try
            {
                return _WndProcImpl(hWnd, msg, wParam, lParam);
            }
            catch (Exception ex)
            {
                string stackTrace = "======= EXCEPTION =======\n" 
                    + ex.ToString();

                Console.Error.WriteLine(stackTrace);
                System.Diagnostics.Debug.Print(stackTrace);
            }
            return 0;
        }

        //----------------------------------------
        static int _WndProcImpl( IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam )
        {
            switch (msg)
            {
                case 0x0001://WM_CREATE
                    break;

                case 0x00FF://WM_INPUT
                    IntPtr hInput = lParam;

                    if (hWnd == s_msgWndForStick)
                    {
                        s_stickInputHandler.ProcessRawInputMessage(hInput);

                        MainWindow mainwnd = App.Current.MainWindow as MainWindow;
                        if (mainwnd != null)
                        {
                            double throttleScale = RawInputJoystickHandler.ScaledThrottlePosition * 1300;
                            double lastHeight = mainwnd.x_greenMask.Height;
                            if (throttleScale < lastHeight - 0.1d || throttleScale > lastHeight + 0.1d)
                            {
                                mainwnd.x_greenMask.Height = throttleScale;

                                Color c = _GetColorForScaledThrottleValue(throttleScale);
                                mainwnd.x_greenMask.Fill = new SolidColorBrush(c);
                            }
                        }
                    }

                    // Per docs, don't call DefWindowProc for background-sink messages.
                    bool backgroundFlag = (wParam != IntPtr.Zero);
                    if (backgroundFlag) return 0;

                    break;

                case 0x0002://WM_DESTROY
                    //Win32.MessageWindow.PostQuitMessage(0);
                    break;
            }

            return Win32.MessageWindow.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        //--------------------------------------------------------------
        // Helpers

        //----------------------------------------
        static Color _GetColorForScaledThrottleValue( double throttleScale )
        {
            // MIL power range => dark-green to lite-green;
            // AB range => bright-yellow to bright-red
            uint red = 0;
            uint green = (uint)(throttleScale * 256 / 1000);
            uint blue = 0;

            if (throttleScale >= 1000d)
            {
                red = 255;
                green = (uint)(256 * (1300 - throttleScale) / (1300 - 1000));
            }

            red = Math.Min(Math.Max(0, red), 255);
            green = Math.Min(Math.Max(0, green), 255);
            blue = Math.Min(Math.Max(0, blue), 255);

            return Color.FromRgb((byte)red, (byte)green, (byte)blue);
        }

    }
}