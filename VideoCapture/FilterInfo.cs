using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace VideoCapture
{
	public class FilterInfo : IComparable
	{
		#region · Data members ·
		public readonly string Name;
		public readonly string MonikerString;
		#endregion

		#region · Constructor and class factory ·

		/// <summary>
		/// Creates filter from moniker string
		/// </summary>
		/// <param name="monikerString"></param>
		public FilterInfo(string monikerString)
		{
			MonikerString = monikerString;
			Name = GetName(monikerString);
		}

		/// <summary>
		/// Creates filter from moniker
		/// </summary>
		/// <param name="moniker"></param>
		internal FilterInfo(IMoniker moniker)
		{
			MonikerString = GetMonikerString(moniker);
			Name = GetName(moniker);
		}
		#endregion

		internal static IBaseFilter CreateFilter(string filterMoniker)
		{
			object filterObject = null;
			IBindCtx bindCtx = null;
			IMoniker moniker = null;

			int n = 0;

			if (CreateBindCtx(0, out bindCtx) == 0)
			{
				if (MkParseDisplayName(bindCtx, filterMoniker, ref n, out moniker) == 0)
				{
					Guid filterId = typeof(IBaseFilter).GUID;
					moniker.BindToObject(null, null, ref filterId, out filterObject);

					Marshal.ReleaseComObject(moniker);
				}
				Marshal.ReleaseComObject(bindCtx);
			}
			return filterObject as IBaseFilter;
		}



		public int CompareTo(object value)
		{
			FilterInfo f = (FilterInfo)value;

			if (f == null)
				return 1;

			return (this.Name.CompareTo(f.Name));
		}


		private string GetMonikerString(IMoniker moniker)
		{
			string str;
			moniker.GetDisplayName(null, null, out str);
			return str;
		}

		private string GetName(IMoniker moniker)
		{
			Object bagObj = null;
			IPropertyBag bag = null;

			try
			{
				Guid bagId = typeof(IPropertyBag).GUID;
				moniker.BindToStorage(null, null, ref bagId, out bagObj);
				bag = (IPropertyBag)bagObj;

				object val = "";
				int hr = bag.Read("FriendlyName", ref val, IntPtr.Zero);
				if (hr != 0)
					Marshal.ThrowExceptionForHR(hr);

				string ret = (string)val;
				if ((ret == null) || (ret.Length < 1))
					throw new ApplicationException();

				return ret;
			}
			catch (Exception)
			{
				return "";
			}
			finally
			{
				bag = null;
				if (bagObj != null)
				{
					Marshal.ReleaseComObject(bagObj);
					bagObj = null;
				}
			}
		}

		private string GetName(string monikerString)
		{
			IBindCtx bindCtx = null;
			IMoniker moniker = null;
			String name = "";
			int n = 0;

			if (CreateBindCtx(0, out bindCtx) == 0)
			{
				if (MkParseDisplayName(bindCtx, monikerString, ref n, out moniker) == 0)
				{
					name = GetName(moniker);

					Marshal.ReleaseComObject(moniker);
					moniker = null;
				}
				Marshal.ReleaseComObject(bindCtx);
				bindCtx = null;
			}
			return name;
		}

		public override string ToString()
		{
			return Name;
		}


		#region · DLL Imports ·
		[DllImport("ole32.dll")]
		public static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

		[DllImport("ole32.dll", CharSet = CharSet.Unicode)]
		public static extern int MkParseDisplayName(IBindCtx pbc, string szUserName, ref int pchEaten, out IMoniker ppmk);
		#endregion
	}
}
