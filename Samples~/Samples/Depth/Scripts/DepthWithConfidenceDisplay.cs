// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.MagicLeap.Samples
{
    /// <summary>
    /// This component displays a picture-in-picture view of the specified AR image.
    /// </summary>
    public sealed class DepthWithConfidenceDisplay : ImageDisplay
    {
        /// <summary>
        /// Name of the max distance property in the shader.
        /// </summary>
        private const string MaxDistanceName = "_MaxDistance";

        /// <summary>
        /// ID of the max distance property in the shader.
        /// </summary>
        private static readonly int MaxDistanceId = Shader.PropertyToID(MaxDistanceName);

        /// <summary>
        /// Name of the max distance property in the shader.
        /// </summary>
        private const string ConfidenceThresholdProperty = "_ConfidenceThreshold";

        /// <summary>
        /// ID of the max distance property in the shader.
        /// </summary>
        private static readonly int ConfidenceThresholdId = Shader.PropertyToID(ConfidenceThresholdProperty);

        /// <summary>
        /// Name of the max distance property in the shader.
        /// </summary>
        private const string ConfidenceTexProperty = "_ConfidenceTex";

        /// <summary>
        /// ID of the max distance property in the shader.
        /// </summary>
        private static readonly int ConfidenceTexId = Shader.PropertyToID(ConfidenceTexProperty);

        [Header("Depth Display Options")]
        [SerializeField]
        [Tooltip("The AROcclusionManager which will produce depth textures.")]
        private AROcclusionManager _occlusionManager;

        [SerializeField]
        [Tooltip("All distances past this value will be displayed as the same color.")]
        private float _maxEnvironmentDistance = 8.0f;

        [Header("Confidence Image Options")]
        [SerializeField]
        private bool _useConfidenceImage = false;

        [SerializeField]
        private Material _materialUsingConfidence;

        // Confidence comes directly from the sensor pipeline and is represented as a float ranging from
        // // [-1.0, 0.0] for long range and [-0.1, 0.0] for short range, where 0 is highest confidence.
        [SerializeField]
        private float _confidenceThreshold = -0.5f;

        // Need to store the last depth image and confidence image for reuse of the same image
        private Texture2D _depthTexture;
        private Texture2D _confidenceTexture;

        private XROcclusionSubsystem _occlusionSubsystem;

        public bool UseConfidenceImage
        {
            get => _useConfidenceImage;
            set
            {
                if (_materialUsingConfidence == null)
                {
                    Log.Error("No material specified for rendering depth with confidence");
                }

                _useConfidenceImage = value;
                _rawImage.material = _useConfidenceImage ? _materialUsingConfidence : _material;
            }
        }

        public float ConfidenceThreshold
        {
            get => _confidenceThreshold;
            set => _confidenceThreshold = value;
        }

        protected override Material ActiveMaterial
        {
            get => _useConfidenceImage ? _materialUsingConfidence : _material;
        }


        protected override void Awake()
        {
            base.Awake();
            Debug.Assert(_occlusionManager != null, "Missing occlusion manager component.");
        }

        /// <summary>
        /// Invoked when it is time to update the presentation.
        /// </summary>
        /// <param name="viewportWidth">The width of the portion of the screen the image will be rendered onto.</param>
        /// <param name="viewportHeight">The height of the portion of the screen the image will be rendered onto.</param>
        /// <param name="orientation">The orientation of the screen.</param>
        /// <param name="renderingMaterial">The material used to render the image.</param>
        /// <param name="image">The image to render.</param>
        /// <param name="displayMatrix">A transformation matrix to fit the image onto the viewport.</param>
        protected override void OnUpdatePresentation
        (
            int viewportWidth,
            int viewportHeight,
            ScreenOrientation orientation,
            Material renderingMaterial,
            out Texture image,
            out Matrix4x4 displayMatrix
        )
        {
            // Update the texture
            image = _occlusionManager.environmentDepthTexture;

            // Calculate the display matrix
            displayMatrix = image != null
                ? CameraMath.CalculateDisplayMatrix(
                    image.width,
                    image.height,
                    viewportWidth,
                    viewportHeight,
                    orientation,
                    invertVertically: true)
                : Matrix4x4.identity;

            // Set custom attributes
            renderingMaterial.SetFloat(MaxDistanceId, _maxEnvironmentDistance);
        }

#if NIANTIC_LIGHTSHIP_ML2_ENABLED
        // This script can be used to display Magic Leap's platform depth image, but because Lightship does
        // not yet support meshing with platform depth, this project has disabled all usage of platform
        // depth to avoid confusion about where it is/is not active.
        private void OnUpdatePresentation_Platform
        (
            int viewportWidth,
            int viewportHeight,
            ScreenOrientation orientation,
            Material renderingMaterial,
            out Texture image,
            out Matrix4x4 displayMatrix
        )
        {
            // MagicLeap does not implement the XROcclusionSubsystem, and instead provides the MLDepthCamera API.
            // For now, Lightship wraps the MLDepthCamera API in the ML2DepthCameraManager class to make it
            // relatively similar to the XRCameraSubsystem API. This may change in the future.
            var success =
                ML2DepthCameraManager.Instance.TryGetDepthCpuImageDeprecated
                (
                    out var depthCpuImage,
                    out var confidenceCpuImage
                );

            if (!success)
            {
                // Use the previously cached texture
                image = _depthTexture;
            }
            else // Apply the depth image to texture
            {
                if (_depthTexture == null)
                {
                    if (depthCpuImage.format != XRCpuImage.Format.DepthFloat32)
                    {
                        Debug.LogError($"Expected depth in DepthFloat32 format but got {depthCpuImage.format}");
                    }

                    _depthTexture =
                        new Texture2D(depthCpuImage.width, depthCpuImage.height, TextureFormat.RFloat, false);
                }

                _depthTexture.LoadRawTextureData(depthCpuImage.GetPlane(0).data);
                _depthTexture.Apply();
                image = _depthTexture;

                // Apply the confidence image to texture
                if (_useConfidenceImage)
                {
                    if (_confidenceTexture == null)
                    {
                        if (confidenceCpuImage.format != XRCpuImage.Format.DepthFloat32)
                        {
                            Debug.LogError($"Expected depth confidence in DepthFloat32 format but got {confidenceCpuImage.format}");
                        }

                        _confidenceTexture =
                            new Texture2D
                            (
                                confidenceCpuImage.width,
                                confidenceCpuImage.height,
                                TextureFormat.RFloat,
                                false
                            );

                        _rawImage.material.SetTexture(ConfidenceTexId, _confidenceTexture);
                    }

                    _confidenceTexture.LoadRawTextureData(confidenceCpuImage.GetPlane(0).data);
                    _confidenceTexture.Apply();

                    _rawImage.material.SetFloat(ConfidenceThresholdId, _confidenceThreshold);
                }

                // Release the cpu images
                depthCpuImage.Dispose();
                confidenceCpuImage.Dispose();
            }

            // Calculate the display matrix
            displayMatrix = image != null
                ? CameraMath.CalculateDisplayMatrix(
                    image.width,
                    image.height,
                    viewportWidth,
                    viewportHeight,
                    ML2CameraManager.Orientation,
                    invertVertically: true)
                : Matrix4x4.identity;
        }
#endif
    }
}
