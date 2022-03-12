/*
 * Interop wrapper for decoding RawInput (WM_INPUT) messages, from joystick devices.
 * (Not implemented: mouse, keyboard)
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Win32
{
    internal partial class RawInput
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
        public static bool DecodeJoystickAxisEvent( IntPtr hRawInput, ushort vendorId, ushort productId, ushort axisId, JoystickAxisHandler onJoystickAxis )
        {
            // Crack raw-input to retrieve device handle and data-buffer; ensure expected HID type.
            _Interop_User32.RawInputHeaderAndRawHid riHeaderAndHid;
            IntPtr refRawInputHeaderBlock = _GetRawInputData_HeaderAndData_Buffered(hRawInput, out riHeaderAndHid);

            IntPtr hDevice = riHeaderAndHid.header.hDevice;
            uint typeEnum = riHeaderAndHid.header.dwType;
            if (typeEnum != _Interop_User32.RIM_TYPEHID)
                return false;

            // Get the vendor- and product-id, for the HID device.
            Tuple<ushort, ushort> pidvid = _GetRawInputDeviceInfo_DevicePidVid_Cached(hDevice);

            if (productId != pidvid.Item1 || vendorId != pidvid.Item2)
                return false;

            // Obtain the "preparsed" data for use with subsequent HID functions.
            IntPtr refPreparsedHidBlock = _GetRawInputDeviceInfo_HidDevicePreparsedData_Cached(hDevice);

            // Get the axis (value) caps -- viz. the logical min/max extents. //TODO: cache this?
            ushort numValueCaps = 1;
            _Interop_Hid.HidP_ValueCaps axisValueCaps = new _Interop_Hid.HidP_ValueCaps();
            {
                uint status = _Interop_Hid.HidP_GetSpecificValueCaps(
                    _Interop_Hid.HidP_Input,
                    _Interop_Hid.HID_USAGE_PAGE_GENERIC, 0, axisId,
                    ref axisValueCaps, ref numValueCaps,
                    refPreparsedHidBlock
                );
                if (status == _Interop_Hid.HIDP_STATUS_USAGE_NOT_FOUND)
                    return false;
                if (status != _Interop_Hid.HIDP_STATUS_SUCCESS)
                    throw new Win32Exception("HidP_GetSpecificValueCaps returned 0x" + status.ToString("X8"));
            }

            //NB: Some devices apparently don't report physicalMin/Max.
            int logMin = (axisValueCaps.logicalMin);
            int logMax = (axisValueCaps.logicalMax);

            // To fetch the actual data for the axis/buttons, we must use ugly untyped pointer-arithmetic 
            // on the buffer we got from GetRawInputData.  The offset to the start of the HID report data
            // begins at the tail end of the RAWHID structure.
            uint rawValue = 0;
            IntPtr refRawDataBuffer = refRawInputHeaderBlock + Marshal.SizeOf<_Interop_User32.RawInputHeaderAndRawHid>();
            {
                uint itemCount = riHeaderAndHid.hid.dwCount;
                uint itemSize = riHeaderAndHid.hid.dwSizeHid;

                uint status = _Interop_Hid.HidP_GetUsageValue(
                    _Interop_Hid.HidP_Input,
                    _Interop_Hid.HID_USAGE_PAGE_GENERIC, 0, axisId,
                    out rawValue,
                    refPreparsedHidBlock,
                    refRawDataBuffer, (itemSize*itemCount)
                );
                if (status == _Interop_Hid.HIDP_STATUS_INCOMPATIBLE_REPORT_ID)
                    return false; //(probably just a button-press with no axis data)
                if (status != _Interop_Hid.HIDP_STATUS_SUCCESS)
                    throw new Win32Exception("HidP_GetUsageValue returned 0x" + status.ToString("X8"));
            }

            // Apply range-scaling, and invoke callback.
            double scaledValue = (double)(rawValue - logMin) / (double)(logMax - logMin);

            scaledValue = Math.Min(Math.Max(0.0d, scaledValue), 1.0d);

            onJoystickAxis(scaledValue);
            return true;
        }


        //--------------------------------------------------------------
        // Managed helpers

        //----------------------------------------
        static IntPtr _GetRawInputData_HeaderAndData_Buffered( IntPtr hRawInput, out _Interop_User32.RawInputHeaderAndRawHid riHeaderAndHid )
        {
            riHeaderAndHid = new _Interop_User32.RawInputHeaderAndRawHid();
            riHeaderAndHid.header.dwType = UInt32.MaxValue;

            // Query header-and-data for HID report (realloc buffer and retry if needed). This 
            // buffer appears to be fixed size but, unlike mouse and keybd, the size is not
            // knowable at compile-time (and it may vary across different HID devices).
            if (true)
            {
                int bufferSize = s_bufferHidHeaderAndReport.MaxSize;
                int status = _Interop_User32.GetRawInputData(hRawInput,
                    _Interop_User32.RID_INPUT,//fetch header-and-data
                    s_bufferHidHeaderAndReport.Pointer, ref bufferSize,
                    _Interop_User32.RawInputHeader.MarshalSize
                );
                if (status <= 0)
                {
                    s_bufferHidHeaderAndReport.Realloc(bufferSize);
                    status = _Interop_User32.GetRawInputData(hRawInput,
                        _Interop_User32.RID_INPUT,//fetch header-and-data
                        s_bufferHidHeaderAndReport.Pointer, ref bufferSize,
                        _Interop_User32.RawInputHeader.MarshalSize
                    );
                    if (status <= 0) throw new Win32Exception();
                }
            }

            // Unpack the header struct from the buffer.
            riHeaderAndHid = Marshal.PtrToStructure<_Interop_User32.RawInputHeaderAndRawHid>(s_bufferHidHeaderAndReport.Pointer);
            return s_bufferHidHeaderAndReport.Pointer;
        }
        static UnmanagedBuffer s_bufferHidHeaderAndReport = new UnmanagedBuffer(1000);

        //----------------------------------------
        static Tuple<ushort,ushort> _GetRawInputDeviceInfo_DevicePidVid_Cached( IntPtr hDevice )
        {
            if (s_cacheHidDeviceInfo.ContainsKey(hDevice))
                return s_cacheHidDeviceInfo[hDevice];

            //NB: For RIDI_DEVICEINFO query, buffer size must be exactly 32 bytes (on x64).
            int bufferSize = _Interop_User32.RawInputDeviceInfo.MarshalSize;
            IntPtr bufferPointer = Marshal.AllocHGlobal(bufferSize);
            if (true)
            {
                int status = _Interop_User32.GetRawInputDeviceInfo(hDevice,
                    _Interop_User32.RIDI_DEVICEINFO,
                    bufferPointer, ref bufferSize
                );
                if (status <= 0) throw new Win32Exception();
            }

            _Interop_User32.RawInputDeviceInfo ridDeviceInfo = Marshal.PtrToStructure<_Interop_User32.RawInputDeviceInfo>(bufferPointer);
            System.Diagnostics.Debug.Assert(ridDeviceInfo.dwSize == _Interop_User32.RawInputDeviceInfo.MarshalSize);
            System.Diagnostics.Debug.Assert(ridDeviceInfo.dwType == _Interop_User32.RIM_TYPEHID);
            System.Diagnostics.Debug.Assert(ridDeviceInfo._union.hid.vendorId <= 0x0000FFFF);
            System.Diagnostics.Debug.Assert(ridDeviceInfo._union.hid.productId <= 0x0000FFFF);

            Tuple<ushort,ushort> pidvid = Tuple.Create<ushort, ushort>(
                (ushort)ridDeviceInfo._union.hid.productId,
                (ushort)ridDeviceInfo._union.hid.vendorId
            );
            s_cacheHidDeviceInfo.Add(hDevice, pidvid);
            System.Diagnostics.Debug.Assert(s_cacheHidDeviceInfo.Count < 20);
            return pidvid;
        }
        static Dictionary<IntPtr, Tuple<ushort,ushort>> s_cacheHidDeviceInfo = new Dictionary<IntPtr, Tuple<ushort,ushort>>(20);

        //----------------------------------------
        static IntPtr _GetRawInputDeviceInfo_HidDevicePreparsedData_Cached( IntPtr hDevice )
        {
            if (s_cacheHidDevicePreparsedData.ContainsKey(hDevice))
                return s_cacheHidDevicePreparsedData[hDevice];

            // Fetch data -- expand buffer and retry if necessary.
            int bufferSize = 4000;
            IntPtr bufferPointer = Marshal.AllocHGlobal(bufferSize);
            if (true)
            {
                int status = _Interop_User32.GetRawInputDeviceInfo(hDevice,
                    _Interop_User32.RIDI_PREPARSEDDATA,
                    bufferPointer, ref bufferSize
                );
                if (status <= 0)
                {
                    bufferPointer = Marshal.ReAllocHGlobal(bufferPointer, (IntPtr)bufferSize);
                    status = _Interop_User32.GetRawInputDeviceInfo(hDevice,
                        _Interop_User32.RIDI_PREPARSEDDATA,
                        bufferPointer, ref bufferSize
                    );
                    if (status <= 0) throw new Win32Exception();
                }
            }
            s_cacheHidDevicePreparsedData.Add(hDevice, bufferPointer);
            System.Diagnostics.Debug.Assert(s_cacheHidDevicePreparsedData.Count < 20);
            return bufferPointer;
        }
        static Dictionary<IntPtr, IntPtr> s_cacheHidDevicePreparsedData = new Dictionary<IntPtr,IntPtr>(20);

    }
}