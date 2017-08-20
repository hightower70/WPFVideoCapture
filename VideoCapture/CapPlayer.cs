using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace VideoCapture
{
	public class CapPlayer : Image, IDisposable, INotifyPropertyChanged
	{
		#region · Data members ·
		private bool m_flip_horizontal;
		private bool m_flip_vertical;
		private CapDevice m_device;
		public static readonly DependencyProperty FramerateProperty =
				DependencyProperty.Register("Framerate", typeof(float), typeof(CapPlayer), new UIPropertyMetadata(default(float)));
		#endregion

		#region · Constructor ·

		/// <summary>
		/// Default constructor
		/// </summary>
		public CapPlayer()
		{
			m_device = new CapDevice();
			m_device.OnNewBitmapReady += new EventHandler(OnNewBitmapReady);

			m_flip_horizontal = false;
			m_flip_vertical = false;
			UpdateFlipTransformation();
		}
		#endregion

		#region · Properties ·

		/// <summary>
		/// Gets calculated framerate
		/// </summary>
		public float Framerate
		{
			get { return (float)GetValue(FramerateProperty); }
			set { SetValue(FramerateProperty, value); }
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

		/// <summary>
		/// Starts video display
		/// </summary>
		public void Start()
		{
			m_device.Start();
		}

		/// <summary>
		/// Stops video display
		/// </summary>
		public void Stop()
		{
			if (m_device != null)
				m_device.Stop();
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
		private void OnNewBitmapReady(object sender, EventArgs e)
		{
			Binding b = new Binding();
			b.Source = m_device;
			b.Path = new PropertyPath(CapDevice.FramerateProperty);
			this.SetBinding(CapPlayer.FramerateProperty, b);

			this.Source = m_device.BitmapSource;
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
	}
}
