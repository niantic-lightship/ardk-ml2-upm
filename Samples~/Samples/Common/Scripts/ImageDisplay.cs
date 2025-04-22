// Copyright 2022-2025 Niantic.
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
        private readonly int _displayMatrixId = Shader.PropertyToID(DisplayMatrixPropertyName);

        /// <summary>
        /// A string builder for construction of strings.
        /// </summary>
        private readonly StringBuilder _stringBuilder = new();

        [SerializeField][Tooltip("The camera rendering the specified RawImage")]
        private Camera _camera;

        [SerializeField]
        protected RawImage _rawImage;

        [SerializeField]
        protected Material _material;

        [SerializeField]
        private Text _imageInfo;

        protected virtual Material ActiveMaterial
        {
            get => _material;
        }

        protected virtual void Awake()
        {
            Debug.Assert(_camera != null, "No camera reference found.");
            Debug.Assert(_rawImage != null, "No raw image reference found.");

            // Initialize the raw image material
            _rawImage.material = ActiveMaterial;
        }

        protected virtual void Update()
        {
            // Inspect the viewport
            var rect = _rawImage.rectTransform.rect;
            var viewportResolution = new Vector2(rect.width, rect.height);

            OnUpdatePresentation
            (
                viewportWidth: (int)viewportResolution.x,
                viewportHeight: (int)viewportResolution.y,
                orientation: XRDisplayContext.GetScreenOrientation(),
                renderingMaterial: _rawImage.material,
                image: out var texture,
                displayMatrix: out var displayMatrix
            );

            // Update the raw image
            _rawImage.texture = texture;
            _rawImage.material.SetMatrix(_displayMatrixId, displayMatrix);

            // Display some text information about each of the textures.
            var displayTexture = _rawImage.texture as Texture2D;
            if (displayTexture != null)
            {
                _stringBuilder.Clear();
                BuildTextureInfo(_stringBuilder, "env", displayTexture);
                LogText(_stringBuilder.ToString());
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
    }
}
