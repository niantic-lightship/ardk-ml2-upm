// Copyright 2022-2024 Niantic.
using System.Text;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Niantic.Lightship.MagicLeap.Samples
{
    public abstract class ImageDisplay : MonoBehaviour
    {
        /// <summary>
        /// Name of the display rotation matrix in the shader.
        /// </summary>
        private const string DisplayMatrixPropertyName = "_DisplayMatrix";

        /// <summary>
        /// ID of the display matrix in the shader.
        /// </summary>
        private readonly int DisplayMatrixId = Shader.PropertyToID(DisplayMatrixPropertyName);

        /// <summary>
        /// A string builder for construction of strings.
        /// </summary>
        protected readonly StringBuilder _stringBuilder = new();

        /// <summary>
        /// The current screen orientation remembered so that we are only updating the raw image layout when it changes.
        /// </summary>
        protected ScreenOrientation _currentScreenOrientation;

        [SerializeField][Tooltip("The camera rendering the specified RawImage")]
        private Camera _camera;

        [SerializeField]
        protected RawImage _rawImage;

        [SerializeField]
        protected Material _material;

        [SerializeField]
        private Text _imageInfo;

        private float _minDimension;

        protected virtual Material ActiveMaterial
        {
            get => _material;
        }

        protected virtual void Awake()
        {
            if (_camera == null)
            {
                Debug.LogError("No camera reference found.");
                return;
            }

            // Get the current screen orientation, and update the raw image UI
            _currentScreenOrientation = XRDisplayContext.GetScreenOrientation();

            // Define the minimum screen size of the raw image based on its editor size
            Vector2 sizeDelta = _rawImage != null ? _rawImage.rectTransform.sizeDelta : new Vector2(480, 480);
            _minDimension = Mathf.Min(sizeDelta.x, sizeDelta.y);
        }

        protected virtual void OnEnable()
        {
            InitializeRawImage();
        }

        protected virtual void Update()
        {
            Debug.Assert(_rawImage != null, "no raw image");

            // Update the image
            var sizeDelta = _rawImage.rectTransform.sizeDelta;
            OnUpdatePresentation
            (
                viewportWidth: (int)sizeDelta.x,
                viewportHeight: (int)sizeDelta.y,
                orientation: _currentScreenOrientation,
                renderingMaterial: _rawImage.material,
                image: out var texture,
                displayMatrix: out var displayMatrix
            );

            _rawImage.texture = texture;
            _rawImage.material.SetMatrix(DisplayMatrixId, displayMatrix);

            if (_rawImage.texture != null)
            {
                // Display some text information about each of the textures.
                var displayTexture = _rawImage.texture as Texture2D;
                if (displayTexture != null)
                {
                    _stringBuilder.Clear();
                    BuildTextureInfo(_stringBuilder, "env", displayTexture);
                    LogText(_stringBuilder.ToString());
                }
            }
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
        protected abstract void OnUpdatePresentation
        (
            int viewportWidth,
            int viewportHeight,
            ScreenOrientation orientation,
            Material renderingMaterial,
            out Texture image,
            out Matrix4x4 displayMatrix
        );

        /// <summary>
        /// Create log information about the given texture.
        /// </summary>
        /// <param name="stringBuilder">The string builder to which to append the texture information.</param>
        /// <param name="textureName">The semantic name of the texture for logging purposes.</param>
        /// <param name="texture">The texture for which to log information.</param>
        private static void BuildTextureInfo(StringBuilder stringBuilder, string textureName, Texture2D texture)
        {
            stringBuilder.AppendLine($"texture : {textureName}");
            if (texture == null)
            {
                stringBuilder.AppendLine("   <null>");
            }
            else
            {
                stringBuilder.AppendLine($"   format : {texture.format}");
                stringBuilder.AppendLine($"   width  : {texture.width}");
                stringBuilder.AppendLine($"   height : {texture.height}");
                stringBuilder.AppendLine($"   mipmap : {texture.mipmapCount}");
            }
        }

        /// <summary>
        /// Log the given text to the screen if the image info UI is set. Otherwise, log the string to debug.
        /// </summary>
        /// <param name="text">The text string to log.</param>
        private void LogText(string text)
        {
            if (_imageInfo != null)
            {
                _imageInfo.text = text;
            }
            else
            {
                Debug.Log(text);
            }
        }

        /// <summary>
        /// Update the raw image with the current configurations.
        /// </summary>
        private void InitializeRawImage()
        {
            Debug.Assert(_rawImage != null, "no raw image");

            // The aspect ratio of the presentation in landscape orientation
            var aspect = Mathf.Max(_camera.pixelWidth, _camera.pixelHeight) /
                (float)Mathf.Min(_camera.pixelWidth, _camera.pixelHeight);

            // Determine the raw image rectSize preserving the texture aspect ratio, matching the screen orientation,
            // and keeping a minimum dimension size.
            float maxDimension = Mathf.Round(_minDimension * aspect);
            Vector2 rectSize;
            switch (_currentScreenOrientation)
            {
                case ScreenOrientation.LandscapeRight:
                case ScreenOrientation.LandscapeLeft:
                    rectSize = new Vector2(maxDimension, _minDimension);
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                case ScreenOrientation.Portrait:
                default:
                    rectSize = new Vector2(_minDimension, maxDimension);
                    break;
            }

            // Update the raw image dimensions and the raw image material parameters.
            _rawImage.rectTransform.sizeDelta = rectSize;
            _rawImage.material = ActiveMaterial;
        }
    }
}
