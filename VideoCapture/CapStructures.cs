using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace VideoCapture
{
	[ComVisible(false)]
	internal enum PinDirection
	{
		Input,
		Output
	}

	/// <summary>
	/// From VideoControlFlags
	/// </summary>
	[Flags]
	public enum VideoControlFlags
	{
		None = 0x0,
		FlipHorizontal = 0x0001,
		FlipVertical = 0x0002,
		ExternalTriggerEnable = 0x0004,
		Trigger = 0x0008
	}

	[ComImport, System.Security.SuppressUnmanagedCodeSecurity,
Guid("6a2e0670-28e4-11d0-a18c-00a0c9118956"),
InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IAMVideoControl
	{
		[PreserveSig]
		int GetCaps(
				[In] IPin pPin,
				[Out] out VideoControlFlags pCapsFlags
				);

		[PreserveSig]
		int SetMode(
				[In] IPin pPin,
				[In] VideoControlFlags Mode
				);

		[PreserveSig]
		int GetMode(
				[In] IPin pPin,
				[Out] out VideoControlFlags Mode
				);

		[PreserveSig]
		int GetCurrentActualFrameRate(
				[In] IPin pPin,
				[Out] out long ActualFrameRate
				);

		[PreserveSig]
		int GetMaxAvailableFrameRate(
				[In] IPin pPin,
				[In] int iIndex,
				[In] Size Dimensions,
				[Out] out long MaxAvailableFrameRate
				);

		[PreserveSig]
		int GetFrameRateList(
				[In] IPin pPin,
				[In] int iIndex,
				[In] Size Dimensions,
				[Out] out int ListSize,
				[Out] out IntPtr FrameRates
				);
	}

	[ComVisible(false), StructLayout(LayoutKind.Sequential)]
	internal class AMMediaType : IDisposable
	{
		public Guid MajorType;

		public Guid SubType;

		[MarshalAs(UnmanagedType.Bool)]
		public bool FixedSizeSamples = true;

		[MarshalAs(UnmanagedType.Bool)]
		public bool TemporalCompression;

		public int SampleSize = 1;

		public Guid FormatType;

		public IntPtr unkPtr;

		public int FormatSize;

		public IntPtr FormatPtr;

		~AMMediaType()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			// remove me from the Finalization queue 
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (FormatSize != 0)
				Marshal.FreeCoTaskMem(FormatPtr);
			if (unkPtr != IntPtr.Zero)
				Marshal.Release(unkPtr);
		}
	}

	[ComVisible(false), StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
	internal class PinInfo
	{
		public IBaseFilter Filter;

		public PinDirection Direction;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string Name;
	}

	[ComVisible(false), StructLayout(LayoutKind.Sequential)]
	internal struct VideoInfoHeader
	{
		public RECT SrcRect;

		public RECT TargetRect;

		public int BitRate;

		public int BitErrorRate;

		public long AverageTimePerFrame;

		public BitmapInfoHeader BmiHeader;
	}

	[ComVisible(false), StructLayout(LayoutKind.Sequential, Pack = 2)]
	internal struct BitmapInfoHeader
	{
		public int Size;

		public int Width;

		public int Height;

		public short Planes;

		public short BitCount;

		public int Compression;

		public int ImageSize;

		public int XPelsPerMeter;

		public int YPelsPerMeter;

		public int ColorsUsed;

		public int ColorsImportant;
	}

	[ComVisible(false), StructLayout(LayoutKind.Sequential)]
	internal struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}
}
