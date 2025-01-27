﻿namespace Unosquare.RaspberryIO.Camera
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Unosquare.Swan.Abstractions;
    using Swan.Components;

    /// <summary>
    /// The Raspberry Pi's camera controller wrapping raspistill and raspivid programs.
    /// This class is a singleton.
    /// </summary>
    public class CameraController : SingletonBase<CameraController>
    {
        #region Private Declarations

        private static readonly ManualResetEventSlim OperationDone = new ManualResetEventSlim(true);
        private static readonly object SyncRoot = new object();
        private static CancellationTokenSource _videoTokenSource = new CancellationTokenSource();
        private static Task<Task> _videoStreamTask;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether the camera module is busy.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is busy; otherwise, <c>false</c>.
        /// </value>
        public bool IsBusy => OperationDone.IsSet == false;

        #endregion

        #region Image Capture Methods

        /// <summary>
        /// Captures an image asynchronously.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="ct">The ct.</param>
        /// <returns>The image bytes.</returns>
        /// <exception cref="InvalidOperationException">Cannot use camera module because it is currently busy.</exception>
        public async Task<byte[]> CaptureImageAsync(CameraStillSettings settings, CancellationToken ct = default)
        {
            if (Instance.IsBusy)
                throw new InvalidOperationException("Cannot use camera module because it is currently busy.");

            if (settings.CaptureTimeoutMilliseconds <= 0)
                throw new ArgumentException($"{nameof(settings.CaptureTimeoutMilliseconds)} needs to be greater than 0");

            try
            {
                OperationDone.Reset();

                var output = new MemoryStream();
                var exitCode = await ProcessRunner.RunProcessAsync(
                    settings.CommandName,
                    settings.CreateProcessArguments(),
                    (data, proc) =>
                    {
                        output.Write(data, 0, data.Length);
                    },
                    null,
                    true,
                    ct).ConfigureAwait(false);

                return exitCode != 0 ? Array.Empty<byte>() : output.ToArray();
            }
            finally
            {
                OperationDone.Set();
            }
        }

        /// <summary>
        /// Captures an image.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns>The image bytes.</returns>
        public byte[] CaptureImage(CameraStillSettings settings)
        {
            return CaptureImageAsync(settings).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Captures a JPEG encoded image asynchronously at 90% quality.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="ct">The ct.</param>
        /// <returns>The image bytes.</returns>
        public Task<byte[]> CaptureImageJpegAsync(int width, int height, CancellationToken ct = default)
        {
            var settings = new CameraStillSettings
            {
                CaptureWidth = width,
                CaptureHeight = height,
                CaptureJpegQuality = 90,
                CaptureTimeoutMilliseconds = 300
            };

            return CaptureImageAsync(settings, ct);
        }

        /// <summary>
        /// Captures a JPEG encoded image at 90% quality.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>The image bytes.</returns>
        public byte[] CaptureImageJpeg(int width, int height) => CaptureImageJpegAsync(width, height).GetAwaiter().GetResult();

        #endregion

        #region Video Capture Methods

        /// <summary>
        /// Opens the video stream with a timeout of 0 (running indefinitely) at 1080p resolution, variable bitrate and 25 FPS.
        /// No preview is shown.
        /// </summary>
        /// <param name="onDataCallback">The on data callback.</param>
        /// <param name="onExitCallback">The on exit callback.</param>
        public void OpenVideoStream(Action<byte[]> onDataCallback, Action onExitCallback = null)
        {
            var settings = new CameraVideoSettings
            {
                CaptureTimeoutMilliseconds = 0,
                CaptureDisplayPreview = false,
                CaptureWidth = 1920,
                CaptureHeight = 1080
            };

            OpenVideoStream(settings, onDataCallback, onExitCallback);
        }

        /// <summary>
        /// Opens the video stream with the supplied settings. Capture Timeout Milliseconds has to be 0 or greater.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="onDataCallback">The on data callback.</param>
        /// <param name="onExitCallback">The on exit callback.</param>
        /// <exception cref="InvalidOperationException">Cannot use camera module because it is currently busy.</exception>
        /// <exception cref="ArgumentException">CaptureTimeoutMilliseconds.</exception>
        public void OpenVideoStream(CameraVideoSettings settings, Action<byte[]> onDataCallback, Action onExitCallback)
        {
            if (Instance.IsBusy)
                throw new InvalidOperationException("Cannot use camera module because it is currently busy.");

            if (settings.CaptureTimeoutMilliseconds < 0)
                throw new ArgumentException($"{nameof(settings.CaptureTimeoutMilliseconds)} needs to be greater than or equal to 0");

            try
            {
                OperationDone.Reset();
                _videoStreamTask = Task.Factory.StartNew(() => VideoWorkerDoWork(settings, onDataCallback, onExitCallback), _videoTokenSource.Token);
            }
            catch
            {
                OperationDone.Set();
                throw;
            }
        }

        /// <summary>
        /// Closes the video stream of a video stream is open.
        /// </summary>
        public void CloseVideoStream()
        {
            lock (SyncRoot)
            {
                if (IsBusy == false)
                    return;
            }

            if (_videoTokenSource.IsCancellationRequested == false)
            {
                _videoTokenSource.Cancel();
                _videoStreamTask.Wait();
            }

            _videoTokenSource = new CancellationTokenSource();
        }

        private static async Task VideoWorkerDoWork(
            CameraVideoSettings settings,
            Action<byte[]> onDataCallback,
            Action onExitCallback)
        {
            try
            {
                await ProcessRunner.RunProcessAsync(
                    settings.CommandName,
                    settings.CreateProcessArguments(),
                    (data, proc) => onDataCallback?.Invoke(data),
                    null,
                    true,
                    _videoTokenSource.Token).ConfigureAwait(false);

                onExitCallback?.Invoke();
            }
            catch
            {
                // swallow
            }
            finally
            {
                Instance.CloseVideoStream();
                OperationDone.Set();
            }
        }
        #endregion
    }
}