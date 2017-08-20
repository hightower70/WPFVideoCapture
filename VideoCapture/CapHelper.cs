using System;
using System.Runtime.InteropServices;

namespace VideoCapture
{
	internal static class CapHelper
	{
		public static IPin GetPinByDirection(this IBaseFilter filter, PinDirection dir, int num)
		{
			IPin[] pin = new IPin[1];
			IEnumPins pinsEnum = null;

			if (filter.EnumPins(out pinsEnum) == 0)
			{
				PinDirection pinDir;
				int n;

				while (pinsEnum.Next(1, pin, out n) == 0)
				{
					pin[0].QueryDirection(out pinDir);

					if (pinDir == dir)
					{
						if (num == 0)
							return pin[0];
						num--;
					}

					Marshal.ReleaseComObject(pin[0]);
					pin[0] = null;
				}
			}
			return null;
		}


		/// <summary>
		/// Scan's a filter's pins looking for a pin with the specified category
		/// </summary>
		/// <param name="vSource">The filter to scan</param>
		/// <param name="guidPinCat">The guid from PinCategory to scan for</param>
		/// <param name="iIndex">Zero based index (ie 2 will return the third pin of the specified category)</param>
		/// <returns>The matching pin, or null if not found</returns>
		public static IPin ByCategory(IBaseFilter vSource, Guid PinCategory, int iIndex)
		{
			int hr;
			IEnumPins ppEnum;
			IPin pRet = null;
			IPin[] pPins = new IPin[1];
			int pins_fetched;

			if (vSource == null)
			{
				return null;
			}

			// Get the pin enumerator
			hr = vSource.EnumPins(out ppEnum);
			if (hr != 0)
				return null;

			try
			{
				// Walk the pins looking for a match
				while (ppEnum.Next(1, pPins, out pins_fetched) == 0)
				{
					// Is it the right category?
					if (GetPinCategory(pPins[0]) == PinCategory)
					{
						// Is is the right index?
						if (iIndex == 0)
						{
							pRet = pPins[0];
							break;
						}
						iIndex--;
					}
					Marshal.ReleaseComObject(pPins[0]);
				}
			}
			finally
			{
				Marshal.ReleaseComObject(ppEnum);
			}

			return pRet;
		}

		/// <summary>
		/// Returns the PinCategory of the specified pin.  Usually a member of PinCategory.  Not all pins have a category.
		/// </summary>
		/// <param name="pPin"></param>
		/// <returns>Guid indicating pin category or Guid.Empty on no category.  Usually a member of PinCategory</returns>
		public static Guid GetPinCategory(IPin pPin)
		{
			Guid guidRet = Guid.Empty;

			// Memory to hold the returned guid
			int iSize = Marshal.SizeOf(typeof(Guid));
			IntPtr ipOut = Marshal.AllocCoTaskMem(iSize);

			try
			{
				int hr;
				int cbBytes;
				Guid g = PropSetID.Pin;

				// Get an IKsPropertySet from the pin
				IKsPropertySet pKs = pPin as IKsPropertySet;

				if (pKs != null)
				{
					// Query for the Category
					hr = pKs.Get(g, (int)AMPropertyPin.Category, IntPtr.Zero, 0, ipOut, iSize, out cbBytes);
					if (hr == 0)
						return Guid.Empty;

					// Marshal it to the return variable
					guidRet = (Guid)Marshal.PtrToStructure(ipOut, typeof(Guid));
				}
			}
			finally
			{
				Marshal.FreeCoTaskMem(ipOut);
				ipOut = IntPtr.Zero;
			}

			return guidRet;
		}

	}
}