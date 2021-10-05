/*
 * Lightweight interop wrapper for a message-only HWND .. useful, eg for receiving WM_USER or 
 * WM_INPUT messages in non-GUI apps.
 */
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Win32
{
    internal class MessageWindow
    {
        const string c_windowClassName = "MessageOnly";
        static IntPtr s_hInstance;
        static WindowProc s_preventWndProcGC;

        //--------------------------------------------------------------
        // Managed wrappers

        //----------------------------------------
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int WindowProc( IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam );

        //----------------------------------------
        public static void InitWindowClass( WindowProc wndProc )
        {
            // Keep a rooted reference to the delegate, to avoid it being reclaimed by GC.
            s_preventWndProcGC = wndProc;

            s_hInstance = Marshal.GetHINSTANCE(typeof(_Interop_User32).Module);

            _Interop_User32.WindowClassEx wcx = _Interop_User32.WindowClassEx.Init(s_hInstance, c_windowClassName, wndProc);
            short classAtom = _Interop_User32.RegisterClassEx(wcx);
            if (classAtom == 0) throw new Win32Exception();

            return;
        }

        //----------------------------------------
        public static IntPtr CreateMessageWindow( )
        {
            if (s_hInstance == IntPtr.Zero || s_preventWndProcGC == null)
                throw new InvalidOperationException();

            IntPtr hwndMessage = new IntPtr(-3);//HWND_MESSAGE
            IntPtr hwnd = _Interop_User32.CreateWindowEx(
                0, c_windowClassName, "Dummy Window for Message Handling", 
                0,//WS_OVERLAPPED
                0, 0, 0, 0,
                hWndParent:hwndMessage,
                IntPtr.Zero,
                hInstance:s_hInstance,
                IntPtr.Zero);

            return hwnd;
        }

        //----------------------------------------
        public static IntPtr PumpMessages()
        {
            _Interop_User32.WindowMessage msg;
            while (0 != _Interop_User32.GetMessage(out msg, IntPtr.Zero, 0, 0))
            {
                _Interop_User32.TranslateMessage(ref msg);
                _Interop_User32.DispatchMessage(ref msg);
            }
            return msg.wParam;
        }

        //----------------------------------------
        public static void PostQuitMessage( int exitCode )
        {
            _Interop_User32.PostQuitMessage(exitCode);
            return;
        }

        //------------------------------
        public static int DefWindowProc( IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam )
        {
            return _Interop_User32.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        //--------------------------------------------------------------
        // Interop declarations

        //----------------------------------------
        // User32 Basic Windowing

        private static class _Interop_User32
        {
            //------------------------------
            // Window class and creation

            [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW")]
            internal static extern short RegisterClassEx( [In] WindowClassEx lpWndClass );

            [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
            internal static extern IntPtr CreateWindowEx(
                uint dwExStyle,
                string lpClassName,
                string lpWindowName,
                uint dwStyle,
                int x,
                int y,
                int nWidth,
                int nHeight,
                IntPtr hWndParent,
                IntPtr hMenu,
                IntPtr hInstance,
                IntPtr lpParam
                );

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal class WindowClassEx//WNDCLASSEX
            {
                public int cbSize = Marshal.SizeOf(typeof(WindowClassEx));

                public int style;
                [MarshalAs(UnmanagedType.FunctionPtr)] public WindowProc lpfnWndProc;
                public int cbClsExtra;
                public int cbWndExtra;
                public IntPtr hInstance;
                public IntPtr hIcon;
                public IntPtr hCursor;
                public IntPtr hbrBackground;
                public string lpszMenuName;
                public string lpszClassName;
                public IntPtr hSmIcon;

                internal static WindowClassEx Init( IntPtr hInstance, string className, WindowProc wndProc )
                {
                    WindowClassEx wcx = new WindowClassEx();
                    wcx.hInstance = hInstance;
                    wcx.lpszClassName = className;
                    wcx.lpfnWndProc = wndProc;
                    return wcx;
                }
            }

            //------------------------------
            // Message pumping

            [DllImport("User32.dll", SetLastError = true)]
            internal static extern int GetMessage( out WindowMessage msg, IntPtr hWnd, uint filterMin, uint filterMax );

            [DllImport("User32.dll", SetLastError = false)]
            internal static extern int TranslateMessage( [In] ref WindowMessage msg );

            [DllImport("User32.dll", SetLastError = false)]
            internal static extern IntPtr DispatchMessage( [In] ref WindowMessage msg );

            [StructLayout(LayoutKind.Sequential)]
            internal struct WindowMessage//MSG
            {
                internal IntPtr hWnd;
                internal uint message;
                internal IntPtr wParam;
                internal IntPtr lParam;
                internal int time;
                internal int ptX;
                internal int ptY;
                internal int lPrivate;
            }

            //------------------------------
            // Message handling

            [DllImport("User32.dll", SetLastError = false, CharSet = CharSet.Unicode, EntryPoint = "DefWindowProcW")]
            internal static extern int DefWindowProc( IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam );

            [DllImport("User32.dll", SetLastError = false)]
            internal static extern void PostQuitMessage( int exitCode );
        }

    }
}