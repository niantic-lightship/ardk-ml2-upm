// Copyright 2022-2025 Niantic.

#if NIANTIC_LIGHTSHIP_ML2_ENABLED

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Niantic.Lightship.AR.PAM;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Utilities.Profiling;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.MagicLeap;

namespace Niantic.Lightship.MagicLeap
{
    public sealed class ML2CameraManager : AsyncService<ML2CameraManager>
    {
        #region Constants

        // The CV camera stream is best for Computer vision scenarios, uncompressed, raw frames.
        // However, if you use this to do CV in you application, you will not be able to perform
        // image or marker tracking using the ML SDK.
        private const MLCamera.Identifier CameraIdentifier = MLCamera.Identifier.CV;
        private const MLCamera.CaptureType VideoCaptureType = MLCamera.CaptureType.Video;
        private const MLCamera.CaptureFrameRate CaptureFrameRate = MLCamera.CaptureFrameRate._30FPS;

        // The largest image size requested by Lightship's modules is 720x540, which is a 4:3 aspect ratio.
        // The smallest image size with compatible aspect ratio provided by the ML CV camera is 1280x960.
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 960;

        public const ScreenOrientation Orientation = ScreenOrientation.LandscapeLeft;

        #endregion

        // Components
        private MLCamera _mlCamera;
        private InputDevice _inputDevice;

        // Resources
        private MLCamera.CameraOutput _latestCameraOutput;
        private MLCamera.ResultExtras _latestResultExtras;
        private readonly GCHandle[] _pinnedDataArray = new GCHandle[3];

        // Helpers
        private long _lastTextureID;
        private readonly MLCamera.OutputFormat _outputFormat = MLCamera.OutputFormat.YUV_420_888;

        /// <summary>
        /// Whether the camera permission has been granted.
        /// </summary>
        public bool IsCameraPermissionGranted { get; private set; }

        /// <summary>
        /// Whether the camera is currently capturing video.
        /// </summary>
        public bool IsCapturingVideo { get; private set; }

        /// <summary>
        /// The current tracking state of the camera.
        /// </summary>
        public TrackingState TrackingState { get; private set; } = TrackingState.None;

        /// <summary>
        /// The timestamp of the latest color camera frame in milliseconds.
        /// </summary>
        public long LastTimestampMs
        {
            // Convert from nanoseconds to milliseconds
            get => _latestResultExtras.VCamTimestamp != null ? _latestResultExtras.VCamTimestamp / 1_000_000 : 0;
        }

        /// <summary>
        /// The name of this service.
        /// </summary>
        protected override string ServiceName
        {
            get => "ML2CameraManager";
        }

        /// <summary>
        /// Attempt to start the camera manager.
        /// </summary>
        protected override async Task<bool> OnStarting(CancellationToken cancellation)
        {
            var isMagicLeapDeviceReady = await WaitForMagicLeapDevice(cancellation);
            if (!isMagicLeapDeviceReady)
            {
                Log.Error("MLDevice is not initialized. Cannot start camera manager.");
                return false;
            }

            var isInputDeviceAvailable = await WaitForInputDevice(cancellation);
            if (!isInputDeviceAvailable)
            {
                Log.Error("Could not acquire HMD input device.");
                return false;
            }

            IsCameraPermissionGranted = await RequestCameraPermission(cancellation);
            if (!IsCameraPermissionGranted)
            {
                return false;
            }

            var isCameraAvailable = await WaitForCameraAvailabilityAsync(cancellation);
            if (!isCameraAvailable)
            {
                return false;
            }

            // Initialize the camera
            var isConnected = await ConnectAndConfigureCameraAsync();
            if (!isConnected)
            {
                return false;
            }

            // Start video capture
            IsCapturingVideo = await StartVideoCaptureAsync();
            if (!IsCapturingVideo)
            {
                Log.Error("Could not start capture. Disconnecting Camera.");

                // Revert initialization
                await DisconnectCameraAsync();
                return false;
            }

            // Subscribe to camera events
            _mlCamera.OnRawVideoFrameAvailable += MLCamera_OnRawVideoFrameAvailable;

            return true;
        }

        /// <summary>
        /// Invoked when the capture is stopping.
        /// </summary>
        /// <returns></returns>
        protected override async Task<bool> OnStopping()
        {
            // Unsubscribe from camera events
            if (IsCapturingVideo && _mlCamera != null)
            {
                _mlCamera.OnRawVideoFrameAvailable -= MLCamera_OnRawVideoFrameAvailable;
            }

            // Stop video capture
            await DisconnectCameraAsync();

            // Release resources
            FreeHandles();
            _latestCameraOutput = default;
            _latestResultExtras = default;

            return true;
        }

        /// <summary>
        /// Invoked when the service has successfully started.
        /// </summary>
        protected override void OnStarted()
        {
            MLDevice.RegisterApplicationPause(OnApplicationPause);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        /// <summary>
        /// Invoked when the service has successfully stopped.
        /// </summary>
        protected override void OnStopped()
        {
            MLDevice.UnregisterApplicationPause(OnApplicationPause);
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        /// <summary>
        ///  Invoked when the application is paused or resumed.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            // Clear cache to prevent using outdated data when the application resumes
            _latestCameraOutput = default;
            _latestResultExtras = default;
        }

        private void OnActiveSceneChanged(Scene arg0, Scene arg1)
        {
            // Clear cache to prevent using outdated data when loading a scene takes too long
            _latestCameraOutput = default;
            _latestResultExtras = default;
        }

        /// <summary>
        /// Invoked when a new raw video frame is available.
        /// </summary>
        private void MLCamera_OnRawVideoFrameAvailable
        (
            MLCamera.CameraOutput output,
            MLCamera.ResultExtras extras,
            MLCamera.Metadata metadataHandle
        )
        {
            // Cache frame data
            _latestCameraOutput = output;
            _latestResultExtras = extras;

            // Update tracking state
            UpdateTrackingState();
        }

        // Called by LightshipMagicLeapCameraSubsystem
        public bool TryGetCameraTextureDescriptor(out XRTextureDescriptor xrTextureDescriptor)
        {
            const string mainTexture = "_MainTex";
            const string name = "TryGetCameraTextureDescriptor";
            ProfilerUtility.EventBegin(ServiceName, name);

            Log.Debug("No RGB texture, early exit from TryGetCameraTextureDescriptor");
            ProfilerUtility.EventEnd(ServiceName, name);
            xrTextureDescriptor = default;
            return false;
        }

        public bool TryGetPose(out Matrix4x4 extrinsics)
        {
            // In case the query is called before the first frame is available
            if (_latestResultExtras.VCamTimestamp == null)
            {
                extrinsics = default;
                return false;
            }

            // Acquire the pose for the latest camera image
            return MLCVCamera.GetFramePose(_latestResultExtras.VCamTimestamp, out extrinsics).IsOk;
        }

        public bool TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics, out double[] distortion)
        {
            if (_latestResultExtras.Intrinsics != null)
            {
                var intrinsics = _latestResultExtras.Intrinsics.Value;
                cameraIntrinsics = new XRCameraIntrinsics
                (
                    intrinsics.FocalLength,
                    intrinsics.PrincipalPoint,
                    new Vector2Int((int)intrinsics.Width, (int)intrinsics.Height)
                );

                distortion = intrinsics.Distortion;
                return true;
            }

            cameraIntrinsics = default;
            distortion = default;
            return false;
        }

        // Currently only used in ML2SubsystemsDataAcquirer to get LightshipCpuImage directly from this class
        // instead of the XRCameraSubsystem
        // Note: LightshipCpuImages returned by this call are only valid up until the next call of this function.
        public bool TryGetLightshipCpuImage(out LightshipCpuImage cpuImage)
        {
            const string name = "TryGetLightshipCpuImage";
            ProfilerUtility.EventBegin(ServiceName, name);

            var hasImageInCache = _latestCameraOutput.Planes is {Length: > 0} &&
                _latestCameraOutput.Planes[0].DataPtr != IntPtr.Zero;

            // No images available or the image is not in a format we can handle
            if (!hasImageInCache || _latestCameraOutput.Planes.Length > 3)
            {
                cpuImage = default;
                return false;
            }

            FreeHandles(); // TODO(bevangelista) Should we avoid silently releasing resources? Two gets would fail

            cpuImage = new LightshipCpuImage(_latestCameraOutput.Format.FromMagicLeapToArdk(),
                _latestCameraOutput.Planes[0].Width, _latestCameraOutput.Planes[0].Height);

            for (int i = 0; i < _latestCameraOutput.Planes.Length; ++i)
            {
                // For YUV_420_888 format only, we only use first two planes for Nv12
                if (_latestCameraOutput.Format == MLCameraBase.OutputFormat.YUV_420_888 && i == 2)
                {
                    break;
                }

                var mlPlane = _latestCameraOutput.Planes[i];
                _pinnedDataArray[i] = GCHandle.Alloc(mlPlane.Data, GCHandleType.Pinned);

                cpuImage.Planes[i] = new LightshipCpuImagePlane(
                    _pinnedDataArray[i].AddrOfPinnedObject(),
                    mlPlane.Size,
                    mlPlane.PixelStride,
                    mlPlane.Stride
                );
            }

            ProfilerUtility.EventEnd(ServiceName, name);
            return true;
        }

        private void UpdateTrackingState()
        {
            if (_inputDevice.isValid &&  InputSubsystem.Extensions.MLHeadTracking.TryGetStateEx(_inputDevice,
                    out InputSubsystem.Extensions.MLHeadTracking.StateEx mlState))
            {
                TrackingState = mlState.Status switch
                {
                    InputSubsystem.Extensions.MLHeadTracking.HeadTrackingStatus.Relocalizing => TrackingState.Limited,
                    InputSubsystem.Extensions.MLHeadTracking.HeadTrackingStatus.Valid => TrackingState.Tracking,
                    _ => TrackingState.None
                };
            }
        }

        private static bool TryAcquireInputDevice(out InputDevice device)
        {
            var result = InputSubsystem.Utils.FindMagicLeapDevice
            (
                InputDeviceCharacteristics.HeadMounted | InputDeviceCharacteristics.TrackedDevice
            );

            device = result.isValid ? result : default;
            return result.isValid;
        }

        private void FreeHandles()
        {
            foreach (var handle in _pinnedDataArray)
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }

        #region Capture logic

        /// <summary>
        /// Delays the execution until the Magic Leap device is ready.
        /// </summary>
        private async Task<bool> WaitForMagicLeapDevice(CancellationToken cancellation)
        {
            const float timeout = 10.0f;
            var startTime = Time.unscaledTime;
            bool deviceReady;
            while (true)
            {
                deviceReady = MLDevice.Instance != null && MLDevice.IsReady();
                var timedOut = Time.unscaledTime - startTime > timeout;

                if (deviceReady || timedOut)
                {
                    break;
                }

                Log.Warning("Magic Leap XR Loader is not initialized. Waiting for it to be ready.");
                await Task.Delay(TimeSpan.FromSeconds(1.0f), cancellation);
            }

            return deviceReady;
        }

        /// <summary>
        /// Delays the execution until the HMD input device is acquired.
        /// </summary>
        private async Task<bool> WaitForInputDevice(CancellationToken cancellation)
        {
            const float maxAttempts = 3;
            for (int i = 0; i < maxAttempts; i++)
            {
                // Acquire the HMD input device
                if (TryAcquireInputDevice(out _inputDevice))
                {
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(1.0f), cancellation);
            }

            return _inputDevice.isValid;
        }

        /// <summary>
        /// Requests the camera permission from the user.
        /// </summary>
        private async Task<bool> RequestCameraPermission(CancellationToken cancellation)
        {
            bool? isCameraPermissionGranted = null;

            // Request camera permission
            PermissionRequester.RequestPermission
            (
                permission: Permission.Camera,
                onPermissionDenied: name =>
                {
                    Log.Error("Permissions denied: " + name);
                    isCameraPermissionGranted = false;
                },
                onPermissionDeniedAndDontAskAgain: name =>
                {
                    Log.Error("Permissions denied: " + name);
                    isCameraPermissionGranted = false;
                },
                onPermissionGranted: name =>
                {
                    Log.Info("Permissions granted: " + name);
                    isCameraPermissionGranted = true;
                }
            );

            // Wait until we get an answer from the user
            while (!isCameraPermissionGranted.HasValue)
            {
                await Task.Delay(TimeSpan.FromSeconds(1.0f), cancellation);
            }

            return isCameraPermissionGranted.GetValueOrDefault(false);
        }

        /// <summary>
        /// Connects the MLCamera component and instantiates a new instance
        /// if it was never created.
        /// </summary>
        private async Task<bool> ConnectAndConfigureCameraAsync()
        {
            _mlCamera ??= await MLCamera.CreateAndConnectAsync(CreateCameraContext());
            if (_mlCamera == null)
            {
                Log.Error("Could not create or connect to a valid camera. Stopping Capture.");
                return false;
            }

            bool hasImageStreamCapabilities = GetStreamCapabilityWBestFit(out MLCameraBase.StreamCapability streamCapability);
            if (!hasImageStreamCapabilities)
            {
                Log.Error("Could not start capture. No valid Image Streams available. Disconnecting Camera.");
                await DisconnectCameraAsync();
                return false;
            }

            // Try to configure the camera based on our target configuration values
            MLCameraBase.CaptureConfig captureConfig = CreateCaptureConfig(streamCapability);
            var prepareResult = _mlCamera.PrepareCapture(captureConfig, out MLCameraBase.Metadata _);
            if (!MLResult.DidNativeCallSucceed(prepareResult.Result, nameof(_mlCamera.PrepareCapture)))
            {
                Log.Error($"Could not prepare capture. Result: {prepareResult.Result} .  Disconnecting Camera.");
                await DisconnectCameraAsync();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Starts the video capture on the MLCamera component.
        /// </summary>
        private async Task<bool> StartVideoCaptureAsync()
        {
            if (_mlCamera == null)
            {
                Log.Error("Could not start video capture. No camera connected.");
                return false;
            }

            // Trigger auto exposure and white balance
            await _mlCamera.PreCaptureAEAWBAsync();

            var startCapture = await _mlCamera.CaptureVideoStartAsync();
            var success = MLResult.DidNativeCallSucceed(startCapture.Result, nameof(_mlCamera.CaptureVideoStart));
            if (!success)
            {
                Debug.LogError($"Could not start camera capture. Result : {startCapture.Result}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Disconnects the MLCamera component and sets it to null.
        /// </summary>
        private async Task DisconnectCameraAsync()
        {
            if (_mlCamera != null)
            {
                if (IsCapturingVideo)
                {
                    await _mlCamera.CaptureVideoStopAsync();
                }

                await _mlCamera.DisconnectAsync();
            }

            _mlCamera = null;
            IsCapturingVideo = false;
        }

        /// <summary>
        /// Waits for the camera device to become available.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> WaitForCameraAvailabilityAsync(CancellationToken cancellationToken)
        {
            bool cameraDeviceAvailable = false;
            int maxAttempts = 10;
            int attempts = 0;

            while (!cameraDeviceAvailable && attempts < maxAttempts)
            {
                MLResult result =
                    MLCameraBase.GetDeviceAvailabilityStatus(CameraIdentifier, out cameraDeviceAvailable);
                if (result.IsOk == false && cameraDeviceAvailable == false)
                {
                    // Wait until the camera device is available
                    await Task.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken);
                }

                attempts++;
            }

            return cameraDeviceAvailable;
        }

        /// <summary>
        /// Gets the Image stream capabilities.
        /// </summary>
        /// <returns>True if MLCamera returned at least one stream capability.</returns>
        private bool GetStreamCapabilityWBestFit(out MLCameraBase.StreamCapability streamCapability)
        {
            streamCapability = default;
            if (_mlCamera == null)
            {
                Debug.Log("Could not get Stream capabilities Info. No Camera Connected");
                return false;
            }

            var streamCapabilities = MLCameraBase.GetImageStreamCapabilitiesForCamera(_mlCamera, VideoCaptureType);
            if (streamCapabilities.Length <= 0)
            {
                return false;
            }

            if (MLCameraBase.TryGetBestFitStreamCapabilityFromCollection(streamCapabilities, CaptureWidth,
                    CaptureHeight, VideoCaptureType, out streamCapability))
            {
                Debug.Log($"Stream: {streamCapability} selected with best fit.");
                return true;
            }

            Debug.Log($"No best fit found. Stream: {streamCapabilities[0]} selected by default.");
            streamCapability = streamCapabilities[0];
            return true;
        }

        private MLCameraBase.CaptureConfig CreateCaptureConfig(MLCameraBase.StreamCapability streamCapability)
        {
            return new MLCameraBase.CaptureConfig
            {
                CaptureFrameRate = CaptureFrameRate,
                StreamConfigs = new[] {MLCamera.CaptureStreamConfig.Create(streamCapability, _outputFormat)}
            };
        }

        private static MLCameraBase.ConnectContext CreateCameraContext()
        {
            var context = MLCamera.ConnectContext.Create();
            context.Flags = MLCamera.ConnectFlag.CamOnly;
            context.CamId = CameraIdentifier;
            context.EnableVideoStabilization = true;
            return context;
        }

        #endregion
    }
}
#endif  // NIANTIC_LIGHTSHIP_ML2_ENABLED
