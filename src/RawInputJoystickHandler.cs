using System;
using System.Configuration;
using System.Globalization;

namespace ThrottleOverlay
{
    internal class RawInputJoystickHandler
    {
        Win32.RawInput.JoystickAxisHandler _onStickAxisData;

        //--------------------------------------------------------------
        // Interface

        //----------------------------------------
        public static double ScaledThrottlePosition = 0d;

        internal static ushort ThrottleAxisVendorId = 0xBAAD;
        internal static ushort ThrottleAxisProductId = 0xF00D;
        internal static ushort ThrottleAxisId = 54;//slider
        internal static bool ThrottleAxisReversed = false;

        //----------------------------------------
        public RawInputJoystickHandler( IntPtr hWnd )
        {
            ScaledThrottlePosition = 0.5d;

            string configThrottleAxis = ConfigurationManager.AppSettings["ThrottleAxis"];
            string[] parts = configThrottleAxis.Split(',');

            ThrottleAxisProductId = UInt16.Parse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            ThrottleAxisVendorId = UInt16.Parse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            ThrottleAxisId = UInt16.Parse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture);

            string configThrottleAxisReversed = ConfigurationManager.AppSettings["ThrottleAxisReversed"];
            ThrottleAxisReversed = Boolean.Parse(configThrottleAxisReversed);

            _onStickAxisData = OnStickAxisData;

            const ushort usagePage = 0x0001;//HID_USAGE_PAGE_GENERIC
            const ushort usageJoystick= 0x0004;//HID_USAGE_GENERIC_JOYSTICK
            Win32.RawInput.RegisterWindowForRawInput(hWnd, usagePage, usageJoystick);
        }

        //----------------------------------------
        public void ProcessRawInputMessage( IntPtr hRawInput )
        {
#if DEBUG
            //Win32.RawInput.LogJoystickEvent(hRawInput);
#endif

            // Decode and track the hardware button-state report.
            Win32.RawInput.DecodeJoystickAxisEvent(hRawInput, ThrottleAxisVendorId, ThrottleAxisProductId, ThrottleAxisId, _onStickAxisData);
            return;
        }

        //--------------------------------------------------------------
        // Implementation

        //----------------------------------------
        internal void OnStickAxisData( double scaledValue )
        {
            // Some axes are reversed for whatever historical reasons..
            if (ThrottleAxisReversed)
                scaledValue = (scaledValue - 1.0) * -1;

            // Constrain range to [0.0-1.0].
            scaledValue = Math.Min(Math.Max(0d, scaledValue), 1d);

            ScaledThrottlePosition = scaledValue;
            return;
        }

    }
}
