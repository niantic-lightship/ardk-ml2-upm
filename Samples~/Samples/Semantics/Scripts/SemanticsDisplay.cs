// Copyright 2022-2024 Niantic.
using System.Linq;
using Niantic.Lightship.AR.Semantics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.MagicLeap.Samples
{
    /// <summary>
    /// This component displays an overlay of semantic classification data.
    /// </summary>
    public sealed class SemanticsDisplay : ImageDisplay
    {
        [SerializeField]
        [Tooltip("The ARSemanticSegmentationManager which will produce semantics textures.")]
        private ARSemanticSegmentationManager _semanticsManager;

        [SerializeField]
        private Dropdown _channelDropdown;

        // The name of the currently selected semantic channel
        private string _semanticChannelName = string.Empty;

        protected override void Awake()
        {
            base.Awake();
            Debug.Assert(_semanticsManager != null, "Missing semantic segmentation manager component.");
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _semanticsManager.MetadataInitialized += OnSemanticsMetadataInitialized;

            if (_channelDropdown is not null)
            {
                _channelDropdown.onValueChanged.AddListener(OnChanelDropdownValueChanged);
            }
        }

        private void OnDisable()
        {
            _semanticsManager.MetadataInitialized -= OnSemanticsMetadataInitialized;

            if (_channelDropdown is not null)
            {
                _channelDropdown.onValueChanged.RemoveListener(OnChanelDropdownValueChanged);
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
            // Use the XRCameraParams type to describe the viewport to fit the semantics image to
            var viewport = new XRCameraParams
            {
                screenWidth = viewportWidth, screenHeight = viewportHeight, screenOrientation = orientation
            };

            // Update the texture with the confidence values of the currently selected channel
            image = _semanticsManager.GetSemanticChannelTexture(_semanticChannelName, out displayMatrix, viewport);
        }

        /// <summary>
        /// Invoked when the semantic segmentation model is downloaded and ready for use.
        /// </summary>
        private void OnSemanticsMetadataInitialized(ARSemanticSegmentationModelEventArgs args)
        {
            // Initialize the channel names in the dropdown menu.
            var channelNames = _semanticsManager.ChannelNames;

            // Display artificial ground by default.
            _semanticChannelName = channelNames[3];

            if (_channelDropdown is not null)
            {
                _channelDropdown.AddOptions(channelNames.ToList());

                var dropdownList = _channelDropdown.options.Select(option => option.text).ToList();
                _channelDropdown.value = dropdownList.IndexOf(_semanticChannelName);
            }
        }

        /// <summary>
        /// Callback when the semantic channel dropdown UI has a value change.
        /// </summary>
        private void OnChanelDropdownValueChanged(int val)
        {
            // Update the display channel from the dropdown value.
            _semanticChannelName = _channelDropdown.options[val].text;
        }
    }
}
