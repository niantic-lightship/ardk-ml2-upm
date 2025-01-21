// Copyright 2022-2024 Niantic.

#if NIANTIC_LIGHTSHIP_ML2_ENABLED

using Niantic.Lightship.AR.PAM;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Input = Niantic.Lightship.AR.Input;
using System;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using MagicLeap.OpenXR.Features;

namespace Niantic.Lightship.MagicLeap
{
    internal class ML2SubsystemsDataAcquirer : SubsystemsDataAcquirer
    {
        private XRInputSubsystem _inputSubsystem;

        public ML2SubsystemsDataAcquirer()
        {
            // On Magic Leap, we need to set the tracking space to unbounded,
            // otherwise the RGB camera image origin will not align with the tracking space origin.
            var referenceSpaceFeature = OpenXRSettings.Instance.GetFeature<MagicLeapReferenceSpacesFeature>();
            if (!referenceSpaceFeature.enabled)
            {
                Log.Error("Unbounded Tracking Space cannot be set if the OpenXR " +
                    "Magic Leap Reference Spaces Feature is not enabled. Stopping Script.");
            }

            // Need to set up spoofed location service early
            TryStartLocation();
            StartCompassIfNeeded();


            // Start the ML2 camera manager
            ML2CameraManager.Instance.Start();
        }

        protected override bool DidLoadSubsystems
        {
            get => _inputSubsystem != null && base.DidLoadSubsystems;
        }

        protected override void OnSubsystemsLoaded(XRLoader loader)
        {
            base.OnSubsystemsLoaded(loader);

            _inputSubsystem = loader.GetLoadedSubsystem<XRInputSubsystem>();
            if (_inputSubsystem == null)
            {
                Log.Error
                (
                    $"No active {typeof(XRInputSubsystem).FullName} is available. " +
                    "Please ensure that a valid loader configuration exists in the XR project settings."
                );

                return;
            }

            // Set the tracking space to unbounded
            if (_inputSubsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Unbounded))
            {
                _inputSubsystem.TryRecenter();
            }
            else
            {
                string currentSpace = _inputSubsystem.GetTrackingOriginMode().ToString();
                Log.Error($"Failed to set tracking origin mode to Unbounded. Current Space:{currentSpace}");
            }
        }

        private static PlatformAdapterManager CreateML2Pam
        (
            IntPtr contextHandle,
            bool isLidarDepthEnabled,
            bool trySendOnUpdate
        )
        {
            return PlatformAdapterManager.Create<AR.PAM.NativeApi, ML2SubsystemsDataAcquirer>
                (
                    contextHandle,
                    isLidarDepthEnabled,
                    trySendOnUpdate
                );
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterPamCreator()
        {
            LightshipUnityContext.CreatePamWithPlugin += CreateML2Pam;
        }

        public override void Dispose()
        {
            LightshipUnityContext.CreatePamWithPlugin -= CreateML2Pam;
            ML2CameraManager.Instance.Stop();
            base.Dispose();
        }

        public override TrackingState GetTrackingState()
        {
            var state = ML2CameraManager.Instance.TrackingState;
            var baseState = base.GetTrackingState();
            Log.Debug($"[ARDK ML] Tracking state: {state}, base state: {baseState}");
            return state;
        }

        public override ScreenOrientation GetScreenOrientation()
        {
            return ML2CameraManager.Orientation;
        }

        public override bool TryGetCameraTimestampMs(out double timestampMs)
        {
            timestampMs = ML2CameraManager.Instance.LastTimestampMs;
            return timestampMs != 0;
        }

        public override bool TryGetCameraIntrinsicsCStruct(out CameraIntrinsicsCStruct intrinsics)
        {
            intrinsics = new CameraIntrinsicsCStruct();
            if (ML2CameraManager.Instance.TryGetIntrinsics(out var result, out _))
            {
                intrinsics.SetIntrinsics(result.focalLength, result.principalPoint, result.resolution);
                return true;
            }

            return false;
        }

        public override bool TryGetCameraPose(out Matrix4x4 pose)
        {
            // Acquire the pose for the latest camera image
            return ML2CameraManager.Instance.TryGetPose(out pose);
        }

        public override bool TryGetCpuImage(out LightshipCpuImage cpuImage)
        {
            return ML2CameraManager.Instance.TryGetLightshipCpuImage(out cpuImage);
        }

        public override bool TryGetDepthCpuImage
        (
            out LightshipCpuImage depthCpuImage,
            out LightshipCpuImage confidenceCpuImage
        )
        {
            // Do not use ML2 depth if it is not enabled.
            if (!LightshipSettingsHelper.ActiveSettings.PreferLidarIfAvailable)
            {
                depthCpuImage = default;
                confidenceCpuImage = default;
                return false;
            }

            return ML2DepthCameraManager.Instance.TryGetDepthCpuImage(out depthCpuImage, out confidenceCpuImage);
        }

        public override bool TryGetDepthCameraIntrinsicsCStruct(out CameraIntrinsicsCStruct depthIntrinsics)
        {
            throw new NotImplementedException();
        }

        public override bool TryGetGpsLocation(out GpsLocationCStruct gps)
        {
            if (Input.location.status != LocationServiceStatus.Running)
            {
                gps = default;
                return false;
            }

            // Uses spoof location service
            gps.TimestampMs = (UInt64)ML2CameraManager.Instance.LastTimestampMs;
            gps.Latitude = Input.location.lastData.latitude;
            gps.Longitude = Input.location.lastData.longitude;
            gps.Altitude = Input.location.lastData.altitude;
            gps.HorizontalAccuracy = Input.location.lastData.horizontalAccuracy;
            gps.VerticalAccuracy = Input.location.lastData.verticalAccuracy;
            return true;
        }

        public override bool TryGetCompass(out CompassDataCStruct compass)
        {
            // Spoof random compass.
            compass.TimestampMs = (UInt64)ML2CameraManager.Instance.LastTimestampMs;
            compass.HeadingAccuracy = -1;  // Negative is unreliable.
            compass.MagneticHeading = 0;
            compass.RawDataX = 0;
            compass.RawDataY = 0;
            compass.RawDataZ = 0;
            compass.TrueHeading = 0;
            return true;
        }
    }
}

#endif  // NIANTIC_LIGHTSHIP_ML2_ENABLED
