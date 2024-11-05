// Copyright 2022-2024 Niantic.

using System.Linq;
using MagicLeap.OpenXR.Features;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Loader;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine.XR.OpenXR;

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
                    Message = "When using Lightship ARDK for Magic Leap, you must add the NIANTIC_LIGHTSHIP_ML2_ENABLED scripting define symbol to your project.",
                    IsRuleEnabled = IsAndroidOpenXREnabled,
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
                    Message = "Lightship features on Magic Leap depend on Feature \"Lightship Magic Leap Features Integration\", which is not enabled.",
                    IsRuleEnabled = IsAndroidOpenXREnabled,
                    CheckPredicate = () =>
                    {
                        var feature = s_settings.GetFeature(typeof(LightshipMagicLeapLoader));
                        return feature is not null && feature.enabled;
                    },
                    FixIt = () =>
                    {
                        var feature = s_settings.GetFeature(typeof(LightshipMagicLeapLoader));
                        feature.enabled = true;
                    },
                    Error = true,
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "Lightship features on Magic Leap depend on \"Perception Snapshots\"  support, which is not enabled.",
                    IsRuleEnabled = IsLightshipMagicLeapFeatureEnabled,
                    CheckPredicate = () =>
                    {
                        var feature = s_settings.GetFeature<MagicLeapFeature>();
                        return feature != null && feature.enabled && feature.EnablePerceptionSnapshots;
                    },
                    FixIt = () =>
                    {
                        var feature = s_settings.GetFeature<MagicLeapFeature>();
                        if (feature != null)
                        {
                            feature.enabled = true;
                            feature.EnablePerceptionSnapshots = true;
                        }
                    },
                    Error = true
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "Lightship features on Magic Leap depend on Feature \"Magic Leap Reference Spaces\", which is not enabled.",
                    IsRuleEnabled = IsLightshipMagicLeapFeatureEnabled,
                    CheckPredicate = () =>
                    {
                        var feature = s_settings.GetFeature<MagicLeapReferenceSpacesFeature>();
                        return feature != null && feature.enabled;
                    },
                    FixIt = () =>
                    {
                        var feature = s_settings.GetFeature<MagicLeapReferenceSpacesFeature>();
                        if (feature != null)
                        {
                            feature.enabled = true;
                        }
                    },
                    Error = true
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "Magic Leap does not include a GPS or Compass sensor. If you are using Lightship ARDK for Magic Leap, you must enable Location & Compass Spoofing.",
                    IsRuleEnabled = IsLightshipMagicLeapFeatureEnabled,
                    CheckPredicate = () => LightshipSettings.Instance.LocationAndCompassDataSource == LocationDataSource.Spoof,
                    FixIt = () =>
                    {
                        LightshipSettings.Instance.LocationAndCompassDataSource = LocationDataSource.Spoof;
                    },
                    Error = true
                }
            });
        }

        private static bool IsAndroidOpenXREnabled()
        {
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings == null)
            {
                return false;
            }

            var managerSettings = generalSettings.AssignedSettings;
            return managerSettings != null && managerSettings.activeLoaders.Any(loader => loader is OpenXRLoader);
        }

        private static bool IsLightshipMagicLeapFeatureEnabled()
        {
            if (!IsAndroidOpenXREnabled())
            {
                return false;
            }

            var lightshipFeature = s_settings.GetFeature<LightshipMagicLeapLoader>();
            return lightshipFeature != null && lightshipFeature.enabled;
        }
    }
}
