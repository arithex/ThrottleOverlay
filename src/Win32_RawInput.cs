/*
 * Interop wrapper for decoding RawInput (WM_INPUT) messages, from joystick devices.
 * Not implemented: mouse, keyboard
 */
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Win32
{
    internal class RawInput
    {
        //--------------------------------------------------------------
        // Managed wrappers

        //----------------------------------------
        public static void RegisterWindowForRawInput( IntPtr hWnd, ushort usagePage, ushort usageId )
        {
            _Interop_User32.RawInputDevice[] rids = { new _Interop_User32.RawInputDevice() };
            rids[0].usagePage = usagePage;
            rids[0].usageId = usageId;
            rids[0].flags = _Interop_User32.RIDEV_INPUTSINK; //collect input in background
            rids[0].hwndTarget = hWnd;

            if (!_Interop_User32.RegisterRawInputDevices(rids, rids.Length, _Interop_User32.RawInputDevice.MarshalSize))
                throw new Win32Exception();

            return;
        }

        //----------------------------------------
        public delegate void JoystickAxisHandler( double scaledValue );

        //----------------------------------------
        public static bool DecodeJoystickJoystickAxisEvent( IntPtr hRawInput, ushort vendorId, ushort productId, ushort axisId, JoystickAxisHandler onJoystickAxis )
        {
            // Crack raw-input to retrieve device handle and data-buffer; ensure expected HID type.
            _Interop_User32.RawInputHeaderAndRawHid riHeaderAndHid;
            IntPtr refRawInputHeaderBlock = _GetRawInputData_CachedHeaderAndBuffer(hRawInput, out riHeaderAndHid);

            IntPtr hDevice = riHeaderAndHid.header.hDevice;
            uint typeEnum = riHeaderAndHid.header.dwType;
            if (typeEnum != _Interop_User32.RIM_TYPEHID)
                return false;

            // Get the vendor- and product-id, for the HID device.
            ushort vendorId1 = 0xBAAD, productId1 = 0xF00D;
            _GetRawInputDeviceInfo_DevicePidVid(hDevice, out vendorId1, out productId1);

            if (vendorId != vendorId1 || productId != productId1)
                return false;

            // Obtain the "preparse" data for use with subsequent HID functions.
            IntPtr refPreparsedHidBlock = _GetRawInputDeviceInfo_CachedPreparsedData(hDevice);

            // Get the axis (value) caps -- viz. the logical min/max extents.
            ushort numValueCaps = 1;
            _Interop_Hid.HidP_ValueCaps axisValueCaps = new _Interop_Hid.HidP_ValueCaps();
            {
                int status = _Interop_Hid.HidP_GetSpecificValueCaps(
                    _Interop_Hid.HidP_Input,
                    _Interop_Hid.HID_USAGE_PAGE_GENERIC, 0, axisId,
                    ref axisValueCaps, ref numValueCaps,
                    refPreparsedHidBlock
                );
                if (status != _Interop_Hid.HIDP_STATUS_SUCCESS)
                    throw new Win32Exception("HidP_GetSpecificValueCaps returned 0x" + status.ToString("X8"));
            }

            //Q: Is it reasonable to assume these are meant to be unsigned? GetUsageValue returns a uint..
            uint rawMin = (uint)(axisValueCaps.physicalMin);
            uint rawMax = (uint)(axisValueCaps.physicalMax);

            // To fetch the actual data for the axis/buttons, we must use ugly untyped pointer-arithmetic 
            // on the buffer we got from GetRawInputData.  The offset to the start of the HID report data
            // begins at the tail end of the RAWHID structure.
            uint rawValue = 0U;
            IntPtr refRawDataBuffer = refRawInputHeaderBlock + Marshal.SizeOf<_Interop_User32.RawInputHeaderAndRawHid>();
            {
                uint itemCount = riHeaderAndHid.hid.dwCount;
                uint itemSize = riHeaderAndHid.hid.dwSizeHid;

                int status = _Interop_Hid.HidP_GetUsageValue(
                    _Interop_Hid.HidP_Input,
                    _Interop_Hid.HID_USAGE_PAGE_GENERIC, 0, axisId,
                    out rawValue,
                    refPreparsedHidBlock,
                    refRawDataBuffer, (itemSize*itemCount)
                );
                if (status != _Interop_Hid.HIDP_STATUS_SUCCESS)
                    throw new Win32Exception("HidP_GetUsageValue returned 0x" + status.ToString("X8"));
            }

            // Validation and cleanup.
            if (rawValue < rawMin || rawValue > rawMax)
                throw new ArgumentOutOfRangeException("Encountered HID axis value outside min/max range.");

            // Apply physical-range scaling, and invoke callback.
            double scaledValue = (double)(rawValue - rawMin) / (double)(rawMax - rawMin);

            onJoystickAxis(scaledValue);
            return true;
        }

        //--------------------------------------------------------------
        // Managed helpers

        //----------------------------------------
        static IntPtr _GetRawInputData_CachedHeaderAndBuffer( IntPtr hRawInput, 
            out _Interop_User32.RawInputHeaderAndRawHid riHeaderAndHid )
        {
            riHeaderAndHid = new _Interop_User32.RawInputHeaderAndRawHid();
            riHeaderAndHid.header.dwType = UInt32.MaxValue;

            // Alloc the necessary buffer if first time through. This buffer appears to be invariant size, but unlike
            // mouse and keybd, is not knowable at compile-time (it depends on the details of the HID device).
            if (s_cacheHidHeaderAndReportBuffer == IntPtr.Zero)
            {
                s_sizeofHidHeaderAndReportBuffer = 0;

                // Query the buffer size.
                int status = _Interop_User32.GetRawInputData(hRawInput,
                    _Interop_User32.RID_INPUT,//fetch header-and-data
                    IntPtr.Zero, ref s_sizeofHidHeaderAndReportBuffer,
                    _Interop_User32.RawInputHeader.MarshalSize
                );
                if (status != 0)
                    throw new Win32Exception();

                // Allocate and cache the buffer.
                s_cacheHidHeaderAndReportBuffer = Marshal.AllocHGlobal(s_sizeofHidHeaderAndReportBuffer);
            }

            // Fetch the data; verify size and failfast if not as expected.
            System.Diagnostics.Debug.Assert(s_sizeofHidHeaderAndReportBuffer != 0);
            if (true)
            {
                int bufferSize = s_sizeofHidHeaderAndReportBuffer;

                int status = _Interop_User32.GetRawInputData(hRawInput, 
                    _Interop_User32.RID_INPUT,//fetch header-and-data
                    s_cacheHidHeaderAndReportBuffer, ref bufferSize, 
                    _Interop_User32.RawInputHeader.MarshalSize
                );
                if (status == -1) throw new Win32Exception();
                if (bufferSize != s_sizeofHidHeaderAndReportBuffer) 
                    throw new Win32Exception("Buffer size for GetRawInputData changed unexpectedly.");
            }

            // Unpack the header struct from the buffer.
            riHeaderAndHid = Marshal.PtrToStructure<_Interop_User32.RawInputHeaderAndRawHid>(s_cacheHidHeaderAndReportBuffer);
            return s_cacheHidHeaderAndReportBuffer;
        }
        static IntPtr s_cacheHidHeaderAndReportBuffer = IntPtr.Zero;
        static int s_sizeofHidHeaderAndReportBuffer = 0;

        //----------------------------------------
        static void _GetRawInputDeviceInfo_DevicePidVid( IntPtr hDevice, 
            out ushort vendorId, out ushort productId )
        {
            vendorId = productId = 0x0000;

            // Alloc the necessary buffer if first time through.
            if (s_cacheHidDeviceInfoBuffer == IntPtr.Zero)
            {
                s_sizeofHidDeviceInfoBuffer = _Interop_User32.RawInputDeviceInfo.MarshalSize;
                s_cacheHidDeviceInfoBuffer = Marshal.AllocHGlobal(s_sizeofHidDeviceInfoBuffer);
            }

            // Fetch the data; verify size and failfast if not as expected.
            System.Diagnostics.Debug.Assert(s_sizeofHidDeviceInfoBuffer != 0);
            if (true)
            {
                int bufferSize = s_sizeofHidDeviceInfoBuffer;

                int status = _Interop_User32.GetRawInputDeviceInfo(hDevice, _Interop_User32.RIDI_DEVICEINFO,
                    s_cacheHidDeviceInfoBuffer, ref bufferSize
                );
                if (status == -1) throw new Win32Exception();
                if (bufferSize != s_sizeofHidDeviceInfoBuffer)
                    throw new Win32Exception("Buffer size for GetRawInputDeviceInfo changed unexpectedly.");
            }

            _Interop_User32.RawInputDeviceInfo ridDeviceInfo = Marshal.PtrToStructure<_Interop_User32.RawInputDeviceInfo>(s_cacheHidDeviceInfoBuffer);
            System.Diagnostics.Debug.Assert(ridDeviceInfo.dwSize == _Interop_User32.RawInputDeviceInfo.MarshalSize);
            System.Diagnostics.Debug.Assert(ridDeviceInfo.dwType == _Interop_User32.RIM_TYPEHID);
            System.Diagnostics.Debug.Assert(ridDeviceInfo._union.hid.vendorId <= 0x0000FFFF);
            System.Diagnostics.Debug.Assert(ridDeviceInfo._union.hid.productId <= 0x0000FFFF);
            System.Diagnostics.Debug.Assert(ridDeviceInfo._union.hid.usagePage == 0x0001);//HID_USAGE_PAGE_GENERIC
            System.Diagnostics.Debug.Assert(ridDeviceInfo._union.hid.usageId == 0x0004);//HID_USAGE_GENERIC_JOYSTICK

            vendorId = (ushort)ridDeviceInfo._union.hid.vendorId;
            productId = (ushort)ridDeviceInfo._union.hid.productId;
            return;
        }
        static IntPtr s_cacheHidDeviceInfoBuffer = IntPtr.Zero;
        static int s_sizeofHidDeviceInfoBuffer = 0;

        //----------------------------------------
        static IntPtr _GetRawInputDeviceInfo_CachedPreparsedData( IntPtr hDevice )
        {
            // Alloc the necessary buffer if first time through. This buffer appears to be invariant size, but is
            // not knowable at compile-time (it depends on the capabilities of the HID device).
            if (s_cacheHidPreparsedBuffer == IntPtr.Zero)
            {
                s_sizeofHidPreparsedBuffer = 0;

                // Query the buffer size.
                int status = _Interop_User32.GetRawInputDeviceInfo(hDevice, _Interop_User32.RIDI_PREPARSEDDATA,
                    IntPtr.Zero, ref s_sizeofHidPreparsedBuffer
                );
                if (status != 0)
                    throw new Win32Exception();

                // Allocate and cache the buffer.
                s_cacheHidPreparsedBuffer = Marshal.AllocHGlobal(s_sizeofHidPreparsedBuffer);
            }

            // Fetch the data; verify size and failfast if not as expected.
            System.Diagnostics.Debug.Assert(s_sizeofHidPreparsedBuffer != 0);
            if (true)
            {
                int bufferSize = s_sizeofHidPreparsedBuffer;

                int status = _Interop_User32.GetRawInputDeviceInfo(hDevice, _Interop_User32.RIDI_PREPARSEDDATA, 
                    s_cacheHidPreparsedBuffer, ref bufferSize
                );
                if (status == -1) throw new Win32Exception();
                if (bufferSize != s_sizeofHidPreparsedBuffer)
                    throw new Win32Exception("Buffer size for GetRawInputDeviceInfo changed unexpectedly.");
            }
            return s_cacheHidPreparsedBuffer;
        }
        static IntPtr s_cacheHidPreparsedBuffer = IntPtr.Zero;
        static int s_sizeofHidPreparsedBuffer = 0;


        //--------------------------------------------------------------
        // Interop declarations

        //----------------------------------------
        // User32 Raw Input API

        static class _Interop_User32
        {
            internal const uint RIDEV_INPUTSINK = 0x00000100;

            internal const uint RID_INPUT = 0x10000003;
            internal const uint RIM_TYPEMOUSE = 0;
            internal const uint RIM_TYPEHID = 2;

            internal const uint RIDI_PREPARSEDDATA = 0x20000005;
            internal const uint RIDI_DEVICEINFO = 0x2000000B;

            [DllImport("User32.dll", SetLastError = true)]
            internal static extern bool RegisterRawInputDevices(
                [In, MarshalAs(UnmanagedType.LPArray)] RawInputDevice[] pRawInputDevices,
                int numDevices,
                int cbSize
            );

            [DllImport("User32.dll", SetLastError = true)]
            internal static extern int GetRawInputData(
                IntPtr hRawInput,
                uint uiCommand,
                IntPtr pData,//nb: variable-length array for HID report data -- will require ugly pointer-arithmetic :(
                [In, Out] ref int cbSize,
                int cbSizeHeader
            );

            [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetRawInputDeviceInfo")]
            internal static extern int GetRawInputDeviceInfo(
                IntPtr hDevice,
                uint uiCommand,
                IntPtr pData,
                [In, Out] ref int pcbSize
            );

            [StructLayout(LayoutKind.Sequential)]
            internal struct RawInputDevice
            {
                internal ushort usagePage;
                internal ushort usageId;
                internal uint flags;
                internal IntPtr hwndTarget;

                internal static int MarshalSize = Marshal.SizeOf<RawInputDevice>();
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct RawInputHeader
            {
                internal uint dwType;//mouse, keybd, or hid/other
                internal uint dwSize;
                internal IntPtr hDevice;
                internal IntPtr wParam;//nonzero indicates background-sinked message

                internal static int MarshalSize = Marshal.SizeOf<RawInputHeader>();
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct RawInputHeaderAndRawMouse
            {
                internal RawInputHeader header; //dwType==RIM_TYPEMOUSE
                internal RawMouse mouse;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct RawMouse
            {
                internal ushort usFlags;
                private ushort _reserved_padding;
                internal ushort usButtonFlags;
                internal short usButtonData;//nb: actual wheelDelta value is signed, not unsigned
                private uint ulRawButtons;//unused, will be zero for most hardware
                internal int lLastX;
                internal int lLastY;
                internal uint ulExtraInformation;
            }
            //TBD: struct RawInputHeaderAndRawKeybd
            //TBD: struct RawKeyboard

            [StructLayout(LayoutKind.Sequential)]
            internal struct RawInputHeaderAndRawHid
            {
                internal RawInputHeader header; //dwType==RIM_TYPEHID
                internal RawHid hid;

                internal static int MarshalSize = Marshal.SizeOf<RawInputHeaderAndRawHid>();
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct RawHid
            {
                internal uint dwSizeHid;
                internal uint dwCount;

                //[MarshalAs(UnmanagedType.ByValArray, SizeParamIndex=???)]
                //byte[] bRawData;
                //NB: Because of the difficulties marshalling the variable-length embedded array, 
                // which is opaque (untyped) binary data anyway, the bRawData must be referenced
                // via unmanaged pointer-arithmetic, eg: 
                //   pRawInput + sizeof(RawInputHeader) + sizeof(RawHid)
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct RawInputDeviceInfo
            {
                internal uint dwSize;
                internal uint dwType;
                internal RawInputDeviceInfo_union _union;

                internal static readonly int MarshalSize = Marshal.SizeOf<RawInputDeviceInfo>();
            }
            [StructLayout(LayoutKind.Explicit)]
            internal struct RawInputDeviceInfo_union
            {
                //[FieldOffset(0)]
                //internal RawInputDeviceInfoMouse mouse; //NB: smallest of the union, and not needed
                [FieldOffset(0)]
                internal RawInputDeviceInfoKeyboard keyboard;
                [FieldOffset(0)]
                internal RawInputDeviceInfoHid hid;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct RawInputDeviceInfoKeyboard
            {
                //NB: only implemented for size-padding (largest of the three unioned structs in RawInputDeviceInfo)
                private uint type, subtype, keyboardMode, numFunctionKeys, numIndicators, numTotalKeys;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct RawInputDeviceInfoHid
            {
                internal uint vendorId, productId, versionId;
                internal ushort usagePage, usageId;
            }
        }

        //----------------------------------------
        // HID "preparsed" data API

        static class _Interop_Hid
        {
            internal const int HIDP_STATUS_SUCCESS = 0x00110000;

            internal const int HidP_Input = 0;
            internal const ushort HID_USAGE_PAGE_GENERIC= 1;
            internal const ushort HID_USAGE_GENERIC_JOYSTICK = 4;

            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern uint HidP_MaxUsageListLength(//aka "GetMaxCountOfButtons"
                uint reportType, ushort usagePage,
                IntPtr pPreparsedData
            );

            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern int HidP_GetUsages(//aka "GetButtonsCurrentlyPressedInThisHidReport"
                uint reportType, ushort usagePage, ushort linkCollection, 
                [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=4)] ushort[] usageList,
                [In, Out] ref uint usageLength,
                IntPtr pPreparsedData,
                IntPtr refHidReportBuffer,
                uint reportLength
            );

            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern int HidP_GetSpecificValueCaps(//aka "GetMinMaxRangesForAxis"
                uint reportType, ushort usagePage, ushort linkCollection, ushort usage,
                [In, Out] ref HidP_ValueCaps valueCaps, [In, Out] ref ushort numValueCaps,
                IntPtr pPreparsedData
            );

            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern int HidP_GetUsageValue(//aka "GetActualPhysicalValueOfAxis"
                uint reportType, ushort usagePage, ushort linkCollection, ushort usage,
                out uint usageValue,
                IntPtr pPreparsedData,
                IntPtr refHidReportBuffer,
                uint reportLength
            );

            [StructLayout(LayoutKind.Sequential)]
            internal struct HidP_ValueCaps
            {
                internal static int MarshalSize = Marshal.SizeOf<HidP_ValueCaps>();

                internal ushort usagePage;
                internal byte reportId;
                internal byte isAlias;
                internal ushort bitField;
                internal ushort linkCollection;
                internal ushort linkUsage;
                internal ushort linkUsagePage;
                internal byte isRange;
                internal byte isStringRange;
                internal byte isDesignatorRange;
                internal byte isAbsolute;
                internal byte hasNull;
                internal byte reserved1;
                internal ushort bitSize;
                internal ushort reportCount;
                internal ushort reserved2a;
                internal ushort reserved2b;
                internal ushort reserved2c;
                internal ushort reserved2d;
                internal ushort reserved2e;
                internal uint unitsExp;
                internal uint units;
                internal int logicalMin;
                internal int logicalMax;
                internal int physicalMin;
                internal int physicalMax;
                internal HidP_ValueCaps_union union;

                [StructLayout(LayoutKind.Explicit)]
                internal struct HidP_ValueCaps_union
                {
                    [FieldOffset(0)] internal HidP_ValueCaps_Range Range;
                    [FieldOffset(0)] internal HidP_ValueCaps_NotRange NotRange;
                }
                [StructLayout(LayoutKind.Sequential)]
                internal struct HidP_ValueCaps_Range
                {
                    internal ushort usageMin;
                    internal ushort usageMax;
                    internal ushort stringMin;
                    internal ushort stringMax;
                    internal ushort designatorMin;
                    internal ushort designatorMax;
                    internal ushort dataIndexMin;
                    internal ushort dataIndexMax;
                }
                [StructLayout(LayoutKind.Sequential)]
                internal struct HidP_ValueCaps_NotRange
                {
                    internal ushort usageId;
                    internal ushort _reserved3a;
                    internal ushort stringIndex;
                    internal ushort _reserved3b;
                    internal ushort designatorIndex;
                    internal ushort _reserved3c;
                    internal ushort dataIndex;
                    internal ushort _reserved3d;
                }
            }
        }

    }
}