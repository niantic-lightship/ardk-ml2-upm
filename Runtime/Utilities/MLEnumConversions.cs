// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.PAM;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.MagicLeap;

namespace Niantic.Lightship.AR.Utilities
{
    internal static class XREnumConversions
    {
        // Magic Leap2 to ARDK Cpu Image Format
        // Must match native_frame.h
        public static ImageFormatCEnum FromMagicLeapToArdk(
            this MLCameraBase.OutputFormat format)
        {
            switch (format)
            {
                case MLCameraBase.OutputFormat.YUV_420_888:
                    // ML YUV actually returns 3 planes: Y UV VU. We only need the first two as NV12 aka the iOS format.
                    return ImageFormatCEnum.Yuv420_NV12;

                case MLCameraBase.OutputFormat.RGBA_8888:
                    return ImageFormatCEnum.RGBA32;

                case MLCameraBase.OutputFormat.JPEG:
                    Debug.Assert(false, "ML2 Jpeg format is not supported! Unhandled value: " + format);
                    return ImageFormatCEnum.Unknown;
                    break;

                default:
                    Debug.Assert(false, "Did XRCpuImage got updated? Unhandled value: " + format);
                    return ImageFormatCEnum.Unknown;
            }
        }
    }
}
