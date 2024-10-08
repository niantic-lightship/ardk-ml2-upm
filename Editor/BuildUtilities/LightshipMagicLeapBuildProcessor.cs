// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Occlusion;
using Niantic.Lightship.AR.Semantics;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.ARSubsystems;

namespace Niantic.Lightship.MagicLeap.Editor
{
    internal class LightshipMagicLeapBuildProcessor
    {
        private class Preprocessor : IPreprocessBuildWithReport
        {
            public int callbackOrder => 1;

            public void OnPreprocessBuild(BuildReport report)
            {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                PreprocessBuild(report);
#endif
            }

            private void PreprocessBuild(BuildReport report)
            {
#if UNITY_ANDROID && NIANTIC_LIGHTSHIP_ML2_ENABLED
                BuildHelper.AddBackgroundShaderToProject(LightshipOcclusionEffect.KShaderName);
                BuildHelper.AddBackgroundShaderToProject(LightshipSemanticsOverlay.KShaderName);

                foreach (string shaderName in LightshipMagicLeapCameraSubsystem.BackgroundShaderNames)
                {
                    BuildHelper.AddBackgroundShaderToProject(shaderName);
                }
#endif
            }
        }
    }
}
