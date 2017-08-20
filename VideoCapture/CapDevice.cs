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
	internal class CapDevice : DependencyObject, IDisposable
	{

		ManualResetEvent stopSignal;
		Thread worker;
		IGraphBuilder m_filter_graph;
		ISampleGrabber grabber;
		IBaseFilter m_capture_filter, m_grabber_filter;
		IMediaControl control;
		CapGrabber capGrabber;
		static string deviceMoniker;
		IntPtr map;
		IntPtr section;

		public InteropBitmap BitmapSource
		{
			get { return (InteropBitmap)GetValue(BitmapSourceProperty); }
			private set { SetValue(BitmapSourcePropertyKey, value); }
		}

		private static readonly DependencyPropertyKey BitmapSourcePropertyKey =
				DependencyProperty.RegisterReadOnly("BitmapSource", typeof(InteropBitmap), typeof(CapDevice), new UIPropertyMetadata(default(InteropBitmap)));
		public static readonly DependencyProperty BitmapSourceProperty = BitmapSourcePropertyKey.DependencyProperty;



		public float Framerate
		{
			get { return (float)GetValue(FramerateProperty); }
			set { SetValue(FramerateProperty, value); }
		}
		public static readonly DependencyProperty FramerateProperty =
				DependencyProperty.Register("Framerate", typeof(float), typeof(CapDevice), new UIPropertyMetadata(default(float)));




		public CapDevice()
		{
			deviceMoniker = GetDeviceList[0].MonikerString;
			//Start();
		}

		public CapDevice(string moniker)
		{
			deviceMoniker = moniker;

			//Start();
		}

		public void Start()
		{
			if (worker == null)
			{
				capGrabber = new CapGrabber();
				capGrabber.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(capGrabber_PropertyChanged);
				capGrabber.NewFrameArrived += new EventHandler(capGrabber_NewFrameArrived);

				stopSignal = new ManualResetEvent(false);
				worker = new Thread(RunWorker);
				worker.Start();
			}
			else
			{
				Stop();
				Start();
			}
		}

		void capGrabber_NewFrameArrived(object sender, EventArgs e)
		{
			if (this.Dispatcher != null)
			{
				this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, (SendOrPostCallback)delegate
				{
					if (BitmapSource != null)
					{
						BitmapSource.Invalidate();
						UpdateFramerate();
					}
				}, null);
			}
		}

		/// <summary>
		/// The event handler for the <see cref="OnVideoControlFlagsChanged"/> event.
		/// Updates the video capture device with new video control properties.
		/// </summary>
		/// <remarks> This method has been disabled, because it was easier to flip the incoming image
		/// with the CV image flip in ImageProcessing.cs.
		/// The direct show flipping didn't work with some webcams, e.g. the PlayStationEye3 cam or an HP Laptop Webcam</remarks>
		/// <param name="property">The <see cref="VideoControlFlags"/> to be changed</param>
		/// <param name="value">The new value for the property</param>
		private void FlipImage()
		{
			if (m_filter_graph == null)
				return;

			if (m_capture_filter == null)
				return;
			/*
			IAMVideoControl videoControl = (IAMCameraControl)m_capture_filter;
			VideoControlFlags pCapsFlags;

			IPin pPin = CapHelper.ByCategory(m_capture_filter, PinCategory.Capture, 0);
			int hr = m_capture_filter.GetCaps(pPin, out pCapsFlags);

			if (hr != 0)
				return;

			hr = videoControl.GetMode(pPin, out pCapsFlags);

			if (hr != 0)
				return;

			pCapsFlags |= VideoControlFlags.FlipVertical;

			hr = videoControl.SetMode(pPin, pCapsFlags);

			if (hr != 0)
				return;*/
		}

		void capGrabber_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.DataBind, (SendOrPostCallback)delegate
			{
				if (capGrabber.Width != default(int) && capGrabber.Height != default(int))
				{
					uint pcount = (uint)(capGrabber.Width * capGrabber.Height * PixelFormats.Bgr32.BitsPerPixel / 8);
					section = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04, 0, pcount, null);
					map = MapViewOfFile(section, 0xF001F, 0, 0, pcount);
					BitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromMemorySection(section, capGrabber.Width, capGrabber.Height, PixelFormats.Bgr32,
										capGrabber.Width * PixelFormats.Bgr32.BitsPerPixel / 8, 0) as InteropBitmap;
					capGrabber.Map = map;
					FlipImage();
					if (OnNewBitmapReady != null)
						OnNewBitmapReady(this, null);
				}
			}, null);
		}

		void UpdateFramerate()
		{
			frames++;
			if (timer.ElapsedMilliseconds >= 1000)
			{
				Framerate = (float)Math.Round(frames * 1000 / timer.ElapsedMilliseconds);
				timer.Reset();
				timer.Start();
				frames = 0;
			}

		}


		System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
		double frames;

		public void Stop()
		{
			if (IsRunning)
			{
				stopSignal.Set();
				worker.Abort();
				if (worker != null)
				{
					worker.Join();
					Release();
				}
			}
		}

		public bool IsRunning
		{
			get
			{
				if (worker != null)
				{
					if (worker.Join(0) == false)
						return true;

					Release();
				}
				return false;
			}
		}

		void Release()
		{
			worker = null;

			stopSignal.Close();
			stopSignal = null;
		}

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

		void RunWorker()
		{
			try
			{

				m_filter_graph = new FilterGraph() as IGraphBuilder;

				m_capture_filter = FilterInfo.CreateFilter(deviceMoniker);

				grabber = new SampleGrabber() as ISampleGrabber;
				m_grabber_filter = grabber as IBaseFilter;

				m_filter_graph.AddFilter(m_capture_filter, "Video Source");
				m_filter_graph.AddFilter(m_grabber_filter, "grabber");

				using (AMMediaType mediaType = new AMMediaType())
				{
					mediaType.MajorType = MediaTypes.Video;
					mediaType.SubType = MediaSubTypes.RGB32;

					grabber.SetMediaType(mediaType);

					if (m_filter_graph.Connect(m_capture_filter.GetPinByDirection(PinDirection.Output, 0), m_grabber_filter.GetPinByDirection(PinDirection.Input, 0)) >= 0)
					{
						if (grabber.GetConnectedMediaType(mediaType) == 0)
						{
							VideoInfoHeader header = (VideoInfoHeader)Marshal.PtrToStructure(mediaType.FormatPtr, typeof(VideoInfoHeader));
							capGrabber.Width = header.BmiHeader.Width;
							capGrabber.Height = header.BmiHeader.Height;
						}
					}
					m_filter_graph.Render(m_grabber_filter.GetPinByDirection(PinDirection.Output, 0));
					grabber.SetBufferSamples(false);
					grabber.SetOneShot(false);
					grabber.SetCallback(capGrabber, 1);

					IVideoWindow wnd = (IVideoWindow)m_filter_graph;
					wnd.put_AutoShow(false);
					wnd = null;

					control = (IMediaControl)m_filter_graph;
					control.Run();

					while (!stopSignal.WaitOne(0, true))
					{
						Thread.Sleep(10);
					}

					control.StopWhenReady();
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
				grabber = null;
				capGrabber = null;
				control = null;

			}

		}



		[DllImport("kernel32.dll", SetLastError = true)]
		static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpFileMappingAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, uint dwNumberOfBytesToMap);

		public event EventHandler OnNewBitmapReady;

#region IDisposable Members

		public void Dispose()
		{
			Stop();
		}

#endregion
	}
}
