using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;

namespace VideoCapture
{
	public class CapViewer : Image, ISampleGrabberCB, IDisposable, INotifyPropertyChanged
	{
		#region · Data members ·
		private bool m_flip_horizontal;
		private bool m_flip_vertical;
		private CapDevice m_device;
		private float m_frame_rate;
		private int m_frames;
		private DateTime m_prev_timestamp;
		private IntPtr m_map;
		#endregion

		#region · Constructor ·

		/// <summary>
		/// Default constructor
		/// </summary>
		public CapViewer()
		{
			m_device = new CapDevice();
			m_device.SampleProcessor = this;
			m_device.OnBitmapFormatChanged += new EventHandler<BitmapFormatChangedEventArgs>(OnBitmapFormatChanged);

			m_flip_horizontal = false;
			m_flip_vertical = false;
			UpdateFlipTransformation();
		}

		#endregion

		#region · Properties ·

		public int BitmapWidth
		{
			get
			{
				if (m_device.BitmapSource == null)
					return 0;
				else
					return m_device.BitmapSource.PixelWidth;
			}
		}

		public int BitmapHeight
		{
			get
			{
				if (m_device.BitmapSource == null)
					return 0;
				else
					return m_device.BitmapSource.PixelHeight;
			}
		}

		/// <summary>
		/// Gets frame rate
		/// </summary>
		public float FrameRate
		{
			get { return m_frame_rate; }
		}

		/// <summary>
		/// Gets/Sets horizontal image flip
		/// </summary>
		public bool FlipHorizontal
		{
			get { return m_flip_horizontal; }
			set { m_flip_horizontal = value; UpdateFlipTransformation(); }
		}

		/// <summary>
		/// Gets/Sets vertical image flip
		/// </summary>
		public bool FlipVertical
		{
			get { return m_flip_vertical; }
			set { m_flip_vertical = value; UpdateFlipTransformation(); }
		}

		/// <summary>
		/// Gets list of available video devices
		/// </summary>
		public FilterInfo[] DeviceList
		{
			get { return CapDevice.GetDeviceList; }
		}

		#endregion
		
		#region · Public functions ·

		public void SelectDevice(string in_moniker)
		{
			m_device.SetInputDeivice(in_moniker);
		}

		/// <summary>
		/// Starts video display
		/// </summary>
		public void Start()
		{
			m_frames = 0;
			UpdateFrameRate(0);
			m_prev_timestamp = DateTime.Now;
			m_device.Start();
		}

		/// <summary>
		/// Stops video display
		/// </summary>
		public void Stop()
		{
			if (m_device != null)
				m_device.Stop();
			UpdateFrameRate(0);
		}
		#endregion

		#region · ISampleGrabberCB Members ·

		public int SampleCB(double sampleTime, IntPtr sample)
		{
			return 0;
		}

		public int BufferCB(double sampleTime, IntPtr buffer, int bufferLen)
		{
			if (m_map != IntPtr.Zero)
			{
				CopyMemory(m_map, buffer, bufferLen);
				/*
				var target = Map;
				var bytesPerRow = (m_Width * 4);
				var source = buffer + bufferLen - bytesPerRow;

				for (int i = m_Height - 1; i > 0; i--)
				{
					CopyMemory(target, source, bytesPerRow);
					target += bytesPerRow;
					source -= bytesPerRow;
				}*/

				if (this.Dispatcher != null)
				{
					this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, (SendOrPostCallback)delegate
					{
						if (m_device.BitmapSource != null)
						{
							m_device.BitmapSource.Invalidate();
						}
					}, null);
				}

				// update frame rate
				m_frames++;
				DateTime current_time = DateTime.Now;
				double difftime = (current_time - m_prev_timestamp).TotalMilliseconds;
				if (difftime >= 1000)
				{
					m_prev_timestamp = current_time;
					UpdateFrameRate((float)Math.Round(m_frames * 1000 / difftime));
					m_frames = 0;
				}
			}
			return 0;
		}

		#endregion

		#region · Private functions ·

		/// <summary>
		/// Updates flip transformation
		/// </summary>
		/// <param name="in_flip_horizontal"></param>
		/// <param name="in_flip_vertical"></param>
		private void UpdateFlipTransformation()
		{
			if (m_flip_horizontal && !m_flip_vertical)
			{
				// no transformation is necessary
				RenderTransform = null;
				return;
			}

			// create required transformation
			TransformGroup transform_group = new TransformGroup();
			ScaleTransform flip_transformation = new ScaleTransform((m_flip_vertical) ? -1 : 1, (m_flip_horizontal) ? 1 : -1);
			transform_group.Children.Add(flip_transformation);

			RenderTransform = transform_group;
		}

		/// <summary>
		/// New image arrived
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnBitmapFormatChanged(object sender, BitmapFormatChangedEventArgs e)
		{
			Source = m_device.BitmapSource;
			m_map = e.Bitmap;

			OnPropertyChanged("BitmapWidth");
			OnPropertyChanged("BitmapHeight");
		}

		/// <summary>
		/// Updates frame rate property
		/// </summary>
		/// <param name="in_frame_rate"></param>
		private void UpdateFrameRate(float in_frame_rate)
		{
			m_frame_rate = in_frame_rate;
			OnPropertyChanged("FrameRate");
		}

		#endregion

		#region · IDisposable Members ·

		public void Dispose()
		{
			if (m_device != null)
				m_device.Dispose();
		}

		#endregion

		#region · INotifyPropertyChanged Members ·

		public event PropertyChangedEventHandler PropertyChanged;

		void OnPropertyChanged(string name)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(name));
			}
		}

		#endregion

		#region · DLLImport functions ·
		[DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory")]
		private static extern void CopyMemory(IntPtr Destination, IntPtr Source, int Length);
		#endregion
	}
}
