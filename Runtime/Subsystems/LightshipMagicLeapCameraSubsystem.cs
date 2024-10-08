// Copyright 2022-2024 Niantic.

#if NIANTIC_LIGHTSHIP_ML2_ENABLED

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.MagicLeap
{
    public class LightshipMagicLeapCameraSubsystem : XRCameraSubsystem
    {
        private const string BeforeOpaquesBackgroundShaderName = "Unlit/LightshipMagicLeapBackground";

        public static readonly string[] BackgroundShaderNames = new[] { BeforeOpaquesBackgroundShaderName };

        public const string CameraDebugKeyword = "CAMERA_DEBUG";

        private const string ID = "Lightship-MLCameraSubsystem";

        private static LightshipMagicLeapCameraSubsystem s_instance;

        public static LightshipMagicLeapCameraSubsystem Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new LightshipMagicLeapCameraSubsystem();
                }
                return s_instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Log.Info($"[ARDK ML] Registering the {ID} subsystem.");
            var cameraSubsystemCinfo = new XRCameraSubsystemCinfo
            {
                id = ID,
                providerType = typeof(LightshipMagicLeapCameraProvider),
                subsystemTypeOverride = typeof(LightshipMagicLeapCameraSubsystem),
                supportsAverageBrightness = false,
                supportsAverageColorTemperature = false,
                supportsColorCorrection = false,
                supportsDisplayMatrix = true,
                supportsProjectionMatrix = true,
                supportsTimestamp = true,
                supportsCameraConfigurations = false,
                supportsCameraImage = true,
                supportsAverageIntensityInLumens = false,
                supportsFocusModes = false,
                supportsFaceTrackingAmbientIntensityLightEstimation = false,
                supportsFaceTrackingHDRLightEstimation = false,
                supportsWorldTrackingAmbientIntensityLightEstimation = false,
                supportsWorldTrackingHDRLightEstimation = false,
                supportsCameraGrain = false,
            };

            if (!XRCameraSubsystem.Register(cameraSubsystemCinfo))
            {
                Log.Error($"[ARDK ML] Failed to register the {ID} subsystem.");
            }
            else
            {
                Log.Info($"[ARDK ML] Registered the {ID} subsystem.");
            }
        }

        private class LightshipMagicLeapCameraProvider : Provider
        {
            private readonly List<string> _legacyRPEnabledMaterialKeywords = new List<string>();
            private readonly List<string> _legacyRPDisabledMaterialKeywords = new List<string>();

            private readonly int LeftDisplayMatrixId = Shader.PropertyToID("_leftDisplayTransform");
            private readonly int RightDisplayMatrixId = Shader.PropertyToID("_rightDisplayTransform");

            private Camera _mainDisplayCamera;

            private long _lastFrameTimestamp = 0;

            private Material _cameraMaterial;

            public override XRCpuImage.Api cpuImageApi => LightshipCpuImageApi.Instance;

            public override bool permissionGranted => ML2CameraManager.Instance.IsCameraPermissionGranted;

            public override XRSupportedCameraBackgroundRenderingMode requestedBackgroundRenderingMode { get; set; }

            public override XRCameraBackgroundRenderingMode currentBackgroundRenderingMode
            {
                get
                {
                    switch (requestedBackgroundRenderingMode)
                    {
                        case XRSupportedCameraBackgroundRenderingMode.AfterOpaques:
                            return XRCameraBackgroundRenderingMode.AfterOpaques;
                        case XRSupportedCameraBackgroundRenderingMode.BeforeOpaques:
                        case XRSupportedCameraBackgroundRenderingMode.Any:
                            return XRCameraBackgroundRenderingMode.BeforeOpaques;
                        default:
                            return XRCameraBackgroundRenderingMode.None;
                    }
                }
            }

            public override XRSupportedCameraBackgroundRenderingMode supportedBackgroundRenderingMode =>
                XRSupportedCameraBackgroundRenderingMode.Any;

            public override bool TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics)
            {
                return ML2CameraManager.Instance.TryGetIntrinsics(out cameraIntrinsics, out _);
            }

            public override bool TryAcquireLatestCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                cinfo = default;
                return false;
            }

            public override Material cameraMaterial
            {
                get
                {
                    if (_cameraMaterial == null)
                    {
                        _cameraMaterial = CreateCameraMaterial(BeforeOpaquesBackgroundShaderName);
                    }

                    return _cameraMaterial;
                }
            }

            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame)
            {
                cameraFrame = default;

                var curTimestamp = ML2CameraManager.Instance.LastTimestampMs;
                if (curTimestamp == _lastFrameTimestamp)
                {
                    return false;
                }
                _lastFrameTimestamp = curTimestamp;

                // Note: These are the intrinsics of the RGB camera, not the per eye display virtual camera
                if (!ML2CameraManager.Instance.TryGetIntrinsics(out var intrinsics, out var distortion))
                {
                    return false;
                }

                // Code from LightshipPlaybackCameraSubsystem.cs
                const XRCameraFrameProperties props =
                    XRCameraFrameProperties.Timestamp |
                    XRCameraFrameProperties.DisplayMatrix |
                    XRCameraFrameProperties.ProjectionMatrix;

                var cameraImageProjectionMatrix = GetCameraImageProjectionMatrix(intrinsics);
                var projectionMatrix = GetDisplayProjectionMatrix();

                TrySetStereoDisplayMatrices(cameraImageProjectionMatrix, projectionMatrix);

                // needs timestamp in nanoseconds
                cameraFrame = new XRCameraFrame(
                    curTimestamp * 1000_000, // ms * 1000 = microseconds. MicroSecs*1000 = nanoseconds
                    0,
                    0,
                    default,
                    projectionMatrix,
                    cameraImageProjectionMatrix,
                    TrackingState.Tracking,
                    IntPtr.Zero,
                    props,
                    0,
                    0,
                    0,
                    0,
                    default,
                    mainLightDirection: Vector3.forward,
                    default,
                    default,
                    0
                );

                return true;
            }

            public override void GetMaterialKeywords(out List<string> enabledKeywords,
                out List<string> disabledKeywords)
            {
                enabledKeywords = _legacyRPEnabledMaterialKeywords;
                disabledKeywords = _legacyRPDisabledMaterialKeywords;
            }

            private Matrix4x4 GetCameraImageProjectionMatrix(XRCameraIntrinsics cameraIntrinsics)
            {
                var cameraParams = new XRCameraParams();
                cameraParams.zNear = 0.37f;
                cameraParams.zFar = 100.0f;
                cameraParams.screenOrientation = (cameraIntrinsics.resolution.x > cameraIntrinsics.resolution.y) ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
                cameraParams.screenHeight = cameraIntrinsics.resolution.y;
                cameraParams.screenWidth = cameraIntrinsics.resolution.x;

                return CameraMath.CalculateProjectionMatrix(cameraIntrinsics, cameraParams);
            }

            private Matrix4x4 GetDisplayProjectionMatrix()
            {
                if (_mainDisplayCamera == null)
                {
                    _mainDisplayCamera = Camera.main;
                }

                return _mainDisplayCamera.projectionMatrix;
            }

            private void TrySetStereoDisplayMatrices(Matrix4x4 cameraImageProjectionMatrix, Matrix4x4 projectionMatrix)
            {
                if (_cameraMaterial != null && _cameraMaterial.IsKeywordEnabled(CameraDebugKeyword) && _mainDisplayCamera != null)
                {
                    var scaleMatrix = cameraImageProjectionMatrix * projectionMatrix.inverse;
                    Vector2 scaleFactor = new Vector2(scaleMatrix[0, 0], scaleMatrix[1, 1]);

                    // TODO: These values seem to work for Objects a few meters away, but may need to be adjusted for closer objects
                    var eyeTransformPosition = new Vector3(GetMiddleEyeOffset() / 8, 0.01f, 0.0f);

                    Vector2 leftCameraOffsets = new Vector2(
                        (1 - scaleFactor.x) / 2f - eyeTransformPosition.x, // Center with IOD offset
                        (1 - scaleFactor.y) / 2f + eyeTransformPosition.y); // Center with RGB Camera Offset

                    Vector2 rightCameraOffsets = new Vector2(
                        (1 - scaleFactor.x) / 2f + eyeTransformPosition.x, // Center with IOD offset
                        (1 - scaleFactor.y) / 2f + eyeTransformPosition.y); // Center with RGB Camera Offset

                    Matrix4x4 leftTranslation = AffineMath.Translation(leftCameraOffsets);
                    Matrix4x4 rightTranslation = AffineMath.Translation(rightCameraOffsets);

                    var leftProjectionMatrix = _mainDisplayCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                    var rightProjectionMatrix = _mainDisplayCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);

                    var leftDisplay = cameraImageProjectionMatrix * leftProjectionMatrix.inverse * leftTranslation;
                    var rightDisplay = cameraImageProjectionMatrix * rightProjectionMatrix.inverse * rightTranslation;

                    _cameraMaterial.SetMatrix(LeftDisplayMatrixId, leftDisplay);
                    _cameraMaterial.SetMatrix(RightDisplayMatrixId, rightDisplay);
                }
            }

            private float GetMiddleEyeOffset()
            {
                const float defaultValue = 0.032f; // Default value between middle camera and left/right cameras
                if (!InputReader.ActiveDevice.HasValue)
                {
                    return defaultValue;
                }

                var device = InputReader.ActiveDevice.Value;
                if (device.characteristics.HasFlag(InputDeviceCharacteristics.HeadMounted))
                {
                    if (device.TryGetFeatureValue(CommonUsages.leftEyePosition, out var leftEyePosition) &&
                        device.TryGetFeatureValue(CommonUsages.rightEyePosition, out var rightEyePosition))
                    {
                        return Vector3.Distance(leftEyePosition, rightEyePosition) / 2f;
                    }
                }

                return defaultValue;
            }

        } // class LightshipMagicLeapCameraProvider

    } // class LightshipMagicLeapCameraSubsystem

}

#endif  // NIANTIC_LIGHTSHIP_ML2_ENABLED
