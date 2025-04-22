// Copyright 2022-2025 Niantic.

#if NIANTIC_LIGHTSHIP_ML2_ENABLED
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.PAM;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Utilities.Profiling;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.MagicLeap;

namespace Niantic.Lightship.MagicLeap
{
    public sealed class ML2DepthCameraManager : AsyncService<ML2DepthCameraManager>
    {
        // If there are no new depth data frames for the given timeout duration, then
        // GetLatestDepthData will return MLResult.Code.Timeout
        private const ulong GetDepthDataTimeoutMs = 5; // 16.7ms is 60fps (Don't want to block longer than that)

        private const MLDepthCamera.Stream StreamMode = MLDepthCamera.Stream.LongRange;
        private const MLDepthCamera.CaptureFlags CaptureFlags =
            MLDepthCamera.CaptureFlags.DepthImage | MLDepthCamera.CaptureFlags.Confidence;

        // Exposure and FrameRateConfig values copied from MLDepthCameraSettings.Init();
        private static readonly MLDepthCamera.StreamConfig s_longRangeConfig = new()
        {
            Exposure = 1600, Flags = (uint)CaptureFlags, FrameRateConfig = MLDepthCamera.FrameRate.FPS_5
        };

        private static readonly MLDepthCamera.StreamConfig s_shortRangeConfig = new()
        {
            Exposure = 375, Flags = (uint)CaptureFlags, FrameRateConfig = MLDepthCamera.FrameRate.FPS_60
        };

        private GCHandle _pinnedDepthArray;
        private GCHandle _pinnedConfidenceArray;

        protected override string ServiceName
        {
            get => "ML2DepthCameraManager";
        }

        protected override async Task<bool> OnStarting(CancellationToken cancellation)
        {
            if (!LightshipSettingsHelper.ActiveSettings.PreferLidarIfAvailable)
            {
                Log.Error
                (
                    "To utilize the MLDepthCamera for Lightship features, enable the 'Prefer depth sensor' " +
                    "checkbox in the Lightship Settings menu."
                );
                return false;
            }

            bool? didComplete = null;
            PermissionRequester.RequestPermission(
                permission: MLPermission.DepthCamera,
                onPermissionGranted: _ =>
                {
                    if (ConnectToCamera())
                    {
                        didComplete = true;
                        Log.Info("Started MLDepthCamera");
                    }
                    else
                    {
                        didComplete = false;
                        Log.Error($"Failed to start MLDepthCamera");
                    }
                },
                onPermissionDenied: _ =>
                {
                    Log.Error("MLDepthCamera permissions denied.");
                    didComplete = false;
                });

            var attempts = 0;
            while (!didComplete.HasValue && !cancellation.IsCancellationRequested)
            {
                if (attempts++ > 100)
                {
                    didComplete = false;
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(0.1f), cancellation);
            }

            return didComplete.GetValueOrDefault(false);
        }

        protected override void OnStopped()
        {
            MLDepthCamera.Disconnect();
            FreeHandles();
        }

        private void FreeHandles()
        {
            if (_pinnedDepthArray.IsAllocated)
            {
                _pinnedDepthArray.Free();
            }
            if (_pinnedConfidenceArray.IsAllocated)
            {
                _pinnedConfidenceArray.Free();
            }
        }

        private static bool ConnectToCamera()
        {
            var config = new MLDepthCamera.StreamConfig[MLDepthCamera.FrameTypeCount];
            config[(int)MLDepthCamera.FrameType.LongRange] = s_longRangeConfig;
            config[(int)MLDepthCamera.FrameType.ShortRange] = s_shortRangeConfig;

            MLDepthCamera.SetSettings(new MLDepthCamera.Settings
            {
                Streams = StreamMode,
                StreamConfig = config
            });

            var connect = MLDepthCamera.Connect();
            if (connect.IsOk && MLDepthCamera.IsConnected)
            {
                Log.Info($"Connected to MLDepthCamera with stream = {MLDepthCamera.CurrentSettings.Streams}");
                return true;
            }

            Log.Error($"Failed to connect to MLDepthCamera (error: {connect.Result})");
            return false;
        }

        private bool TryGetDepthBuffers
        (
            out MLDepthCamera.FrameBuffer depthBuffer,
            out MLDepthCamera.FrameBuffer confidenceBuffer
        )
        {
            depthBuffer = default;
            confidenceBuffer = default;

            if (!IsRunning)
            {
                return false;
            }

            var result = MLDepthCamera.GetLatestDepthData(GetDepthDataTimeoutMs, out var depthData);
            if (!result.IsOk && result != MLResult.Code.Timeout)
            {
                Log.Error($"Failed to get depth data (error: {result})");
                return false;
            }

            depthBuffer = depthData.DepthImage.GetValueOrDefault();
            confidenceBuffer = depthData.ConfidenceBuffer.GetValueOrDefault();
            return true;
        }

        public bool TryGetDepthCpuImageDeprecated(out XRCpuImage depthCpuImage, out XRCpuImage confidenceCpuImage)
        {
            const string name = "TryGetDepthImageCpuImage";
            ProfilerUtility.EventBegin(ServiceName, name);

            depthCpuImage = default;
            confidenceCpuImage = default;
            if (!TryGetDepthBuffers(out var depthImage, out var confidenceImage))
            {
                return false;
            }

            var addedManagedDepth =
                LightshipCpuImageApi.Instance.TryAddManagedXRCpuImage
                (
                    depthImage.Data,
                    (int)depthImage.Width,
                    (int)depthImage.Height,
                    XRCpuImage.Format.DepthFloat32,
                    (ulong)ML2CameraManager.Instance.LastTimestampMs,
                    out var depthCinfo
                );

            if (!addedManagedDepth)
            {
                Log.Error("Failed to add depth data as a LightshipCpuImage");
                return false;
            }

            var addedManagedConfidence =
                LightshipCpuImageApi.Instance.TryAddManagedXRCpuImage
                (
                    confidenceImage.Data,
                    (int)confidenceImage.Width,
                    (int)confidenceImage.Height,
                    XRCpuImage.Format.DepthFloat32,
                    (ulong)ML2CameraManager.Instance.LastTimestampMs,
                    out var confidenceCinfo
                );

            if (!addedManagedConfidence)
            {
                Log.Error("Failed to add confidence as a LightshipCpuImage");
                return false;
            }

            depthCpuImage = new XRCpuImage(LightshipCpuImageApi.Instance, depthCinfo);
            confidenceCpuImage = new XRCpuImage(LightshipCpuImageApi.Instance, confidenceCinfo);

            ProfilerUtility.EventEnd(ServiceName, name);
            return true;
        }

        public bool TryGetDepthCpuImage(out LightshipCpuImage depthCpuImage, out LightshipCpuImage confidenceCpuImage)
        {
            const string name = "TryGetDepthCpuImage";
            ProfilerUtility.EventBegin(ServiceName, name);

            if (!TryGetDepthBuffers(out var depthImage, out var confidenceImage))
            {
                depthCpuImage = default;
                confidenceCpuImage = default;
                return false;
            }

            FreeHandles();

            depthCpuImage = new LightshipCpuImage(ImageFormatCEnum.DepthFloat32,
                depthImage.Width, depthImage.Height);
            confidenceCpuImage = new LightshipCpuImage(ImageFormatCEnum.DepthFloat32,
                confidenceImage.Width, confidenceImage.Height);

            _pinnedDepthArray = GCHandle.Alloc(depthImage.Data, GCHandleType.Pinned);
            _pinnedConfidenceArray = GCHandle.Alloc(confidenceImage.Data, GCHandleType.Pinned);

            depthCpuImage.Planes[0] = new LightshipCpuImagePlane(
                _pinnedDepthArray.AddrOfPinnedObject(),
                (uint)depthImage.Data.Length,
                depthImage.BytesPerUnit,
                depthImage.Stride
            );

            // TODO [ARDK-3848]: The format of ML2's confidence buffer is different from ARKit's.
            // This needs to be resolved in the SAL pipeline before Lightship can support usage of platform
            // depth for modules.
            confidenceCpuImage.Planes[0] = new LightshipCpuImagePlane(
                _pinnedConfidenceArray.AddrOfPinnedObject(),
                (uint)confidenceImage.Data.Length,
                confidenceImage.BytesPerUnit,
                confidenceImage.Stride
            );

            ProfilerUtility.EventEnd(ServiceName, name);
            return true;
        }

        public bool TryGetDepthTexture(out Texture2D texture)
        {
            if (!TryGetDepthBuffers(out var depthImage, out _))
            {
                texture = null;
                return false;
            }

            texture = new Texture2D((int)depthImage.Width, (int)depthImage.Height, TextureFormat.RFloat, false);
            texture.LoadRawTextureData(depthImage.Data);
            texture.Apply();

            return true;
        }
    }
}
#endif  // NIANTIC_LIGHTSHIP_ML2_ENABLED
