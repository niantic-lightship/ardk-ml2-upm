using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.MagicLeap.Samples
{
    /// <summary>
    /// This component gets the camera image from the ARCameraManager as an XRCpuImage object
    /// and renders it on-screen via the specified RawImage component.
    ///
    /// As it's only intended for debug use, it has restrictions. It expects the image to be of
    /// RGBA32 format, and thus only works with on MagicLeap with Lightship Playback enabled or
    /// NIANTIC_ARDK_USE_FAST_LIGHTWEIGHT_PAM symbol NOT defined
    /// </summary>
    public class CameraImageDisplay : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The ARCameraManager which will produce frame events.")]
        private ARCameraManager _cameraManager;

        [SerializeField]
        private RawImage _rawImage;

        [SerializeField]
        private Text _imageInfo;

        private Texture2D _texture;

        void OnEnable()
        {
            if (_cameraManager != null)
            {
                _cameraManager.frameReceived += OnCameraFrameReceived;
            }
        }

        void OnDisable()
        {
            if (_cameraManager != null)
            {
                _cameraManager.frameReceived -= OnCameraFrameReceived;
            }
        }

        unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
        {
            // Attempt to get the latest camera image. If this method succeeds,
            // it acquires a native resource that must be disposed (see below).
            XRCpuImage image;
            if (!_cameraManager.TryAcquireLatestCpuImage(out image))
            {
                return;
            }

            // Display some information about the camera image
            _imageInfo.text =
                string.Format
                (
                    "Image info:\n\twidth: {0}\n\theight: {1}\n\tplaneCount: {2}\n\ttimestamp: {3}\n\tformat: {4}",
                    image.width, image.height, image.planeCount, image.timestamp, image.format
                );

            // Once we have a valid XRCameraImage, we can access the individual image "planes"
            // (the separate channels in the image). XRCameraImage.GetPlane provides
            // low-overhead access to this data. This could then be passed to a
            // computer vision algorithm. Here, we will convert the camera image
            // to an RGBA texture and draw it on the screen.

            // Choose an RGBA format.
            // See XRCameraImage.FormatSupported for a complete list of supported formats.
            var format = TextureFormat.RGBA32;

            if (_texture == null || _texture.width != image.width || _texture.height != image.height)
            {
                _texture = new Texture2D(image.width, image.height, format, false);
            }

            // Convert the image to format, flipping the image across the Y axis.
            // We can also get a sub rectangle, but we'll get the full image here.
            // Note: No need for ML2
            var conversionParams = new XRCpuImage.ConversionParams(image, format, XRCpuImage.Transformation.MirrorX);

            // Texture2D allows us to write directly to the raw texture data
            // This allows us to do the conversion in-place without making any copies.
            var rawTextureData = _texture.GetRawTextureData<byte>();
            try
            {
                image.Convert(conversionParams, new IntPtr(rawTextureData.GetUnsafePtr()), rawTextureData.Length);
            }
            finally
            {
                // We must dispose of the XRCameraImage after we're finished
                // with it to avoid leaking native resources.
                image.Dispose();
            }

            // Apply the updated texture data to our texture
            _texture.Apply();

            // Set the RawImage's texture so we can visualize it.
            _rawImage.texture = _texture;
        }
    }
}
