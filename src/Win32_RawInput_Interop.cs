/*
 * Interop declarations for Win32_RawInput.cs
 * (Not yet implemented: mouse, keyboard)
 */
using System;
using System.Runtime.InteropServices;

namespace Win32
{
    internal partial class RawInput
    {
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
            internal const uint HIDP_STATUS_SUCCESS = 0x0011_0000;
            internal const uint HIDP_STATUS_USAGE_NOT_FOUND = 0xC011_0004;
            internal const uint HIDP_STATUS_INCOMPATIBLE_REPORT_ID = 0xC011_000A;

            internal const int HidP_Input = 0;
            internal const ushort HID_USAGE_PAGE_GENERIC= 1;
            internal const ushort HID_USAGE_GENERIC_JOYSTICK = 4;

            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern uint HidP_GetCaps(//aka "GetDeviceCapabilities"
                IntPtr pPreparsedData,
                out HidP_Caps deviceCaps
            );

            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern uint HidP_MaxDataListLength(
                uint reportType,
                IntPtr pPreparsedData
            );

            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern uint HidP_GetData(//aka "GetAllTheDataInThisHidReport"
                uint reportType,
                [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] HidP_Data[] dataBlocks,
                [In, Out] ref uint numDataBlocks,
                IntPtr pPreparsedData,
                IntPtr refHidReportBuffer,
                uint reportLength
            );


            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern uint HidP_GetUsages(//aka "GetButtonsCurrentlyPressedInThisHidReport"
                uint reportType, ushort usagePage, ushort linkCollection, 
                [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=4)] ushort[] usageList,
                [In, Out] ref uint usageLength,
                IntPtr pPreparsedData,
                IntPtr refHidReportBuffer,
                uint reportLength
            );


            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern uint HidP_GetValueCaps(//aka "GetMinMaxRangesForAllAxes"
                uint reportType,
                [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] HidP_ValueCaps[] valueCaps,
                [In, Out] ref ushort numValueCaps,
                IntPtr pPreparsedData
            );

            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern uint HidP_GetSpecificValueCaps(//aka "GetMinMaxRangeForAxis"
                uint reportType, ushort usagePage, ushort linkCollection, ushort usage,
                [In, Out] ref HidP_ValueCaps valueCaps, 
                [In, Out] ref ushort numValueCaps,
                IntPtr pPreparsedData
            );

            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern uint HidP_GetScaledUsageValue(//aka "GetActualValueOfAxisScaledToLogicalRange"
                uint reportType, ushort usagePage, ushort linkCollection, ushort usage,
                out int usageValue,
                IntPtr pPreparsedData,
                IntPtr refHidReportBuffer,
                uint reportLength
            );

            [DllImport("Hid.dll", SetLastError = false)]
            internal static extern uint HidP_GetUsageValue(//aka "GetActualPhysicalValueOfAxis"
                uint reportType, ushort usagePage, ushort linkCollection, ushort usage,
                out uint usageValue,
                IntPtr pPreparsedData,
                IntPtr refHidReportBuffer,
                uint reportLength
            );

            [StructLayout(LayoutKind.Sequential)] 
            internal struct HidP_Caps
            {
                internal static int MarshalSize = Marshal.SizeOf<HidP_Caps>();

                internal ushort usage;
                internal ushort usagePage;
                internal ushort inputReportByteLength;
                internal ushort outputReportByteLength;
                internal ushort featureReportByteLength;
                [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U2, SizeConst = 17)] internal ushort[] _reserved;
                internal ushort numberLinkCollectionNodes;
                internal ushort numberInputButtonCaps;
                internal ushort numberInputValueCaps;
                internal ushort numberInputDataIndices;
                internal ushort numberOutputButtonCaps;
                internal ushort numberOutputValueCaps;
                internal ushort numberOutputDataIndices;
                internal ushort numberFeatureButtonCaps;
                internal ushort numberFeatureValueCaps;
                internal ushort numberFeatureDataIndices;
            }

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

            [StructLayout(LayoutKind.Sequential)]
            internal struct HidP_Data
            {
                internal static int MarshalSize = Marshal.SizeOf<HidP_Data>();

                internal ushort dataIndex;
                internal ushort _reserved;
                internal HidP_Data_union union;

                [StructLayout(LayoutKind.Explicit)]
                internal struct HidP_Data_union
                {
                    [FieldOffset(0)] internal uint RawValue;
                    [FieldOffset(0)] internal byte On;
                }
            }
        }

    }
}