// Copyright 2022-2024 Niantic.

using System;
using System.Linq;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Niantic.Lightship.MagicLeap.Editor
{
    internal static class LightshipMagicLeapProjectValidationRules
    {
        private const string Category = "Niantic Lightship MagicLeap Plugin";
        private static readonly OpenXRSettings s_settings = OpenXRSettings.ActiveBuildTargetInstance;

        [InitializeOnLoadMethod]
        private static void AddLightshipMagicLeapValidationRules()
        {
            BuildValidator.AddRules(BuildTargetGroup.Android, new[] {
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK for MagicLeap, you must add the NIANTIC_LIGHTSHIP_ML2_ENABLED scripting define symbol to your project.",
                    IsRuleEnabled = GetAndroidIsOpenXREnabled,
                    CheckPredicate = () => PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android).Contains("NIANTIC_LIGHTSHIP_ML2_ENABLED"),
                    FixIt = () => {
                        var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
                        symbols += ";NIANTIC_LIGHTSHIP_ML2_ENABLED";
                        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, symbols);
                    },
                    Error = true,
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK for MagicLeap, you must enable \"Lightship Magic Leap Features Integration\" in OpenXR settings.",
                    IsRuleEnabled = GetAndroidIsOpenXREnabled,
                    CheckPredicate = () =>
                    {
                        var feature = s_settings.GetFeature(typeof(LightshipMagicLeapLoader));
                        return feature is not null && feature.enabled;
                    },
                    FixIt = () => {
                        var feature = s_settings.GetFeature(typeof(LightshipMagicLeapLoader));
                        feature.enabled = true;
                    },
                    Error = true,
                },
            });
        }

        private static bool GetAndroidIsOpenXREnabled()
        {
            // TODO(lycai): ARDK-4274 check for Lightship enabled checkbox in OpenXR settings.
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings == null)
            {
                return false;
            }

            var managerSettings = generalSettings.AssignedSettings;
            return managerSettings != null && managerSettings.activeLoaders.Any(loader => loader is OpenXRLoader);
        }
    }
}
