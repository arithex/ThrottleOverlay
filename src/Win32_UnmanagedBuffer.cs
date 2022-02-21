/*
 * Helper class for unmanaged IntPtr buffer interop.
 */
using System;
using System.Runtime.InteropServices;

namespace Win32
{
    internal class UnmanagedBuffer : IDisposable
    {
        int _maxsize = 0;
        IntPtr _pointer = IntPtr.Zero;

        //----------------------------------------
        static UnmanagedBuffer( )
        {
#if DEBUG
            Test();
#endif
        }

        //----------------------------------------
        public UnmanagedBuffer( int maxsize )
        {
            if (maxsize <= 0) throw new ArgumentOutOfRangeException("maxsize");

            _pointer = Marshal.AllocHGlobal((int)maxsize);
            _maxsize = maxsize;
        }

        //----------------------------------------
        public int MaxSize
        {
            get { return _maxsize; }
        }

        public IntPtr Pointer
        {
            get {
                if (_pointer == IntPtr.Zero)
                    throw new InvalidOperationException("Use-after-free attempted.");

                return _pointer;
            }
        }

        //----------------------------------------
        public void Realloc( int maxsize )
        {
            if (maxsize <= 0) throw new ArgumentOutOfRangeException("maxsize");

            if (_pointer == IntPtr.Zero)
                throw new InvalidOperationException("Realloc after free not supported.");

            _pointer = Marshal.ReAllocHGlobal(_pointer, (IntPtr)maxsize);
            _maxsize = maxsize;
            return;
        }

        public void Dealloc( )
        {
            if (_pointer == IntPtr.Zero)
                throw new InvalidOperationException("Double-free attempted.");

            Marshal.FreeHGlobal(_pointer);
            _pointer = IntPtr.Zero;
            _maxsize = 0;
            return;
        }

        public IntPtr Detach( )
        {
            if (_pointer == IntPtr.Zero)
                throw new InvalidOperationException("Use-after-free attempted.");

            IntPtr retval = _pointer;
            _pointer = IntPtr.Zero;
            _maxsize = 0;
            return retval;
        }

        void IDisposable.Dispose( )
        {
            Dealloc();
            return;
        }

        //----------------------------------------
        private static void Test( )
        {
            UnmanagedBuffer obj = new UnmanagedBuffer(42);
            {
                System.Diagnostics.Debug.Assert(obj._pointer != IntPtr.Zero);
                System.Diagnostics.Debug.Assert(obj._maxsize!= 0);

                obj.Realloc(420);
                System.Diagnostics.Debug.Assert(obj._pointer != IntPtr.Zero);
                System.Diagnostics.Debug.Assert(obj._maxsize != 0);

                obj.Dealloc();
                System.Diagnostics.Debug.Assert(obj._pointer == IntPtr.Zero);
                System.Diagnostics.Debug.Assert(obj._maxsize == 0);
            }
            return;
        }
    }
}