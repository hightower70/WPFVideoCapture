using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace VideoCapture
{
	internal class BitmapFormatChangedEventArgs : EventArgs
	{
		public IntPtr Bitmap { set; get; }
	}

	internal class CapDevice : DependencyObject, IDisposable
	{
		#region · Data members · 
		private ManualResetEvent m_thread_stop_request;
		private ManualResetEvent m_thread_stopped;
		private Thread m_worker_thread;
		private IGraphBuilder m_filter_graph;
		private ISampleGrabber m_sample_grabber;
		private IBaseFilter m_capture_filter, m_grabber_filter;
		private IMediaControl m_media_control;
		private string m_device_moniker;
		private IntPtr m_bitmap;
		private IntPtr m_section;
		private ISampleGrabberCB m_sample_processor;
		#endregion

		#region  · Properties ·

		public InteropBitmap BitmapSource
		{
			get { return (InteropBitmap)GetValue(BitmapSourceProperty); }
			private set { SetValue(BitmapSourcePropertyKey, value); }
		}

		private static readonly DependencyPropertyKey BitmapSourcePropertyKey =
				DependencyProperty.RegisterReadOnly("BitmapSource", typeof(InteropBitmap), typeof(CapDevice), new UIPropertyMetadata(default(InteropBitmap)));
		public static readonly DependencyProperty BitmapSourceProperty = BitmapSourcePropertyKey.DependencyProperty;


		public ISampleGrabberCB SampleProcessor
		{
			get { return m_sample_processor; }
			set { m_sample_processor = value; }
		}

		#endregion

		#region · Events ·
		public event EventHandler<BitmapFormatChangedEventArgs> OnBitmapFormatChanged;
		#endregion

		#region · Constructor ·

		public CapDevice()
		{
			m_device_moniker = string.Empty;
		}

		public CapDevice(string moniker)
		{
			m_device_moniker = moniker;
		}
		#endregion

		#region · Public functions ·

		/// <summary>
		/// Sets input device for grabbing
		/// </summary>
		/// <param name="in_moniker">Device moniker of the input device</param>
		public void SetInputDeivice(string in_moniker)
		{
			m_device_moniker = in_moniker;
		}

		/// <summary>
		/// Starts video grabbing
		/// </summary>
		public void Start()
		{
			if (m_worker_thread == null)
			{
				m_thread_stop_request = new ManualResetEvent(false);
				m_thread_stopped = new ManualResetEvent(false);
				m_worker_thread = new Thread(RunWorker);
				m_worker_thread.Start();
			}
			else
			{
				Stop();
				Start();
			}
		}

		/// <summary>
		/// Stops video grabbing
		/// </summary>
		public void Stop()
		{
			if (IsRunning)
			{
				// set stop request
				m_thread_stop_request.Set();

				// wait for thread stopped
				if(!m_thread_stopped.WaitOne(1000))
				{
					// force thread exit
					m_worker_thread.Abort();
				}

				// release resources
				Release();
			}
		}

		/// <summary>
		/// Checks if video grabbing is active (returns true if active)
		/// </summary>
		public bool IsRunning
		{
			get
			{
				if (m_worker_thread != null)
				{
					if (m_worker_thread.Join(0) == false)
						return true;

					Release();
				}
				return false;
			}
		}
		
		/// <summary>
		/// Gets available video capture device list
		/// </summary>
		public static FilterInfo[] GetDeviceList
		{
			get
			{
				List<FilterInfo> filters = new List<FilterInfo>();
				IMoniker[] ms = new IMoniker[1];
				ICreateDevEnum enumD = new SystemDeviceEnum() as ICreateDevEnum;
				IEnumMoniker moniker;
				Guid g = FilterCategory.VideoInputDevice;
				if (enumD.CreateClassEnumerator(ref g, out moniker, 0) == 0)
				{

					while (true)
					{
						int r = moniker.Next(1, ms, IntPtr.Zero);
						if (r != 0 || ms[0] == null)
							break;
						filters.Add(new FilterInfo(ms[0]));
						Marshal.ReleaseComObject(ms[0]);
						ms[0] = null;

					}
				}

				return filters.ToArray();
			}
		}

		#endregion

		#region · Private functions ·

		/// <summary>
		/// Changes the current video stream resolution
		/// </summary>
		/// <param name="in_width">New width in pixels</param>
		/// <param name="in_height">New height in pixels</param>
		private void ChangeBitmapFormat(int in_width, int in_height)
		{
			this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.DataBind, (SendOrPostCallback)delegate
			{
				if (in_width != default(int) && in_height != default(int))
				{
					uint pcount = (uint)(in_width * in_height * PixelFormats.Bgr32.BitsPerPixel / 8);
					m_section = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04, 0, pcount, null);
					m_bitmap = MapViewOfFile(m_section, 0xF001F, 0, 0, pcount);
					BitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromMemorySection(m_section, in_width, in_height, PixelFormats.Bgr32,
										in_width * PixelFormats.Bgr32.BitsPerPixel / 8, 0) as InteropBitmap;

					BitmapFormatChangedEventArgs event_arg = new BitmapFormatChangedEventArgs();
					event_arg.Bitmap = m_bitmap;

					if (OnBitmapFormatChanged != null)
						OnBitmapFormatChanged(this, event_arg);
				}
			}, null);
		}

		/// <summary>
		/// Releases resources
		/// </summary>
		void Release()
		{
			m_worker_thread = null;

			m_thread_stopped.Close();
			m_thread_stopped = null;
			m_thread_stop_request.Close();
			m_thread_stop_request = null;
		}

		/// <summary>
		/// Main thread
		/// </summary>
		private void RunWorker()
		{
			try
			{
				m_filter_graph = new FilterGraph() as IGraphBuilder;

				m_capture_filter = FilterInfo.CreateFilter(m_device_moniker);

				m_sample_grabber = new SampleGrabber() as ISampleGrabber;
				m_grabber_filter = m_sample_grabber as IBaseFilter;

				m_filter_graph.AddFilter(m_capture_filter, "Video Source");
				m_filter_graph.AddFilter(m_grabber_filter, "grabber");

				using (AMMediaType mediaType = new AMMediaType())
				{
					mediaType.MajorType = MediaTypes.Video;
					mediaType.SubType = MediaSubTypes.RGB32;

					m_sample_grabber.SetMediaType(mediaType);

					if (m_filter_graph.Connect(m_capture_filter.GetPinByDirection(PinDirection.Output, 0), m_grabber_filter.GetPinByDirection(PinDirection.Input, 0)) >= 0)
					{
						if (m_sample_grabber.GetConnectedMediaType(mediaType) == 0)
						{
							VideoInfoHeader header = (VideoInfoHeader)Marshal.PtrToStructure(mediaType.FormatPtr, typeof(VideoInfoHeader));
							ChangeBitmapFormat(header.BmiHeader.Width, header.BmiHeader.Height);
						}
					}

					m_filter_graph.Render(m_grabber_filter.GetPinByDirection(PinDirection.Output, 0));
					m_sample_grabber.SetBufferSamples(false);
					m_sample_grabber.SetOneShot(false);
					m_sample_grabber.SetCallback(m_sample_processor, 1);

					IVideoWindow wnd = (IVideoWindow)m_filter_graph;
					wnd.put_AutoShow(false);
					wnd = null;

					// start capturing
					m_media_control = (IMediaControl)m_filter_graph;
					m_media_control.Run();

					// wait for end request
					while (!m_thread_stop_request.WaitOne(100))
					{
					}

					// stop capturing
					m_media_control.Stop();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex);
			}
			finally
			{
				m_filter_graph = null;
				m_capture_filter = null;
				m_grabber_filter = null;
				m_sample_grabber = null;
				m_media_control = null;

				// thread is finished
				if(m_thread_stopped != null)
					m_thread_stopped.Set();
			}
		}

		#endregion

		#region · DLL Import ·
		[DllImport("kernel32.dll", SetLastError = true)]
		static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpFileMappingAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, uint dwNumberOfBytesToMap);

		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			Stop();
		}

		#endregion
	}
}
