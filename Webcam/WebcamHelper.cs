using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Drawing;
using System.Threading;

namespace mullvad.Module.RemoteWebcam
{
    public class WebcamHelper
    {
        private readonly object _lock = new object();
        private Bitmap _currentFrame;
        private bool _isRunning = false;
        private VideoCaptureDevice _videoDevice;
        private int _width;
        private int _height;
        private DateTime _lastFrameTime = DateTime.MinValue;
        private readonly TimeSpan _frameInterval = TimeSpan.FromMilliseconds(33); // ~30fps

        public void StartWebcam(int webcamIndex)
        {
            try
            {
                if (_isRunning) return;

                FilterInfoCollection captureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (captureDevices.Count == 0) throw new Exception("No webcam detected.");
                if (webcamIndex < 0 || webcamIndex >= captureDevices.Count) throw new Exception("Invalid device index.");

                _videoDevice = new VideoCaptureDevice(captureDevices[webcamIndex].MonikerString);

                var videoCapabilities = _videoDevice.VideoCapabilities;
                if (videoCapabilities != null && videoCapabilities.Length > 0)
                {
                    bool foundMatch = false;
                    foreach (var cap in videoCapabilities)
                    {
                        if (cap.AverageFrameRate >= 25 && cap.AverageFrameRate <= 35)
                        {
                            _videoDevice.VideoResolution = cap;
                            foundMatch = true;
                            break;
                        }
                    }
                    if (!foundMatch)
                        _videoDevice.VideoResolution = videoCapabilities[0];
                }

                _videoDevice.NewFrame += FinalFrame_NewFrame;
                _videoDevice.VideoSourceError += VideoDevice_VideoSourceError;
                _videoDevice.Start();
                _isRunning = true;
            }
            catch (Exception ex)
            {
                throw new Exception("Error starting webcam: " + ex.Message, ex);
            }
        }

        public static string[] GetWebcams()
        {
            try
            {
                FilterInfoCollection captureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                string[] webcams = new string[captureDevices.Count];
                for (int i = 0; i < captureDevices.Count; i++)
                    webcams[i] = captureDevices[i].Name;
                return webcams;
            }
            catch
            {
                return new string[0];
            }
        }

        public void StopWebcam()
        {
            if (!_isRunning) return;
            try
            {
                _videoDevice.SignalToStop();
                _videoDevice.WaitForStop();
            }
            catch { }
            finally
            {
                if (_videoDevice != null)
                {
                    _videoDevice.NewFrame -= FinalFrame_NewFrame;
                    _videoDevice.VideoSourceError -= VideoDevice_VideoSourceError;
                    _videoDevice = null;
                }
                lock (_lock) { _currentFrame?.Dispose(); _currentFrame = null; }
                _isRunning = false;
            }
        }

        public Bitmap GetLatestFrame()
        {
            lock (_lock)
            {
                try
                {
                    if (_currentFrame == null) return null;
                    DateTime now = DateTime.UtcNow;
                    if ((now - _lastFrameTime) >= _frameInterval)
                    {
                        _lastFrameTime = now;
                        return _currentFrame.Clone() as Bitmap;
                    }
                    return null;
                }
                catch { return null; }
            }
        }

        public bool IsRunning => _isRunning;

        public int Width  { get { lock (_lock) { return _width;  } } }
        public int Height { get { lock (_lock) { return _height; } } }

        private void FinalFrame_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            lock (_lock)
            {
                try
                {
                    _currentFrame?.Dispose();
                    Bitmap frame = (Bitmap)eventArgs.Frame.Clone();
                    frame.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    _currentFrame = frame;
                    _width  = _currentFrame.Width;
                    _height = _currentFrame.Height;
                }
                catch { }
            }
        }

        private void VideoDevice_VideoSourceError(object sender, VideoSourceErrorEventArgs eventArgs)
        {
            var desc = eventArgs.Description ?? string.Empty;
            // Ignore "interface not supported" — benign with some virtual cameras
            if (desc.IndexOf("0x80004002", StringComparison.OrdinalIgnoreCase) >= 0 ||
                desc.IndexOf("Interface not supported", StringComparison.OrdinalIgnoreCase) >= 0)
                return;
        }
    }
}
