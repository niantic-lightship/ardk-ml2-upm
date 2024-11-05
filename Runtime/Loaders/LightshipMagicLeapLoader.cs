// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using MagicLeap.OpenXR.Features;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities.Logging;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR.Features;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEditor.Build;
using UnityEngine.XR.OpenXR;
#endif // UNITY_EDITOR

namespace Niantic.Lightship.MagicLeap
{
#if UNITY_EDITOR
    [OpenXRFeatureSet(
        UiName = "Niantic Lightship support for Magic Leap",
        Description = "Features to use Lightship functionality on the Magic Leap 2.",
        FeatureSetId = "com.nianticlabs.lightship.ml2.featuregroup",
        SupportedBuildTargets = new [] { BuildTargetGroup.Android },
        FeatureIds = new[] { LightshipMagicLeapLoader.FeatureID },
        DefaultFeatureIds = new[] { LightshipMagicLeapLoader.FeatureID }
    )]
    public class LightshipMagicLeapFeatureGroup { }

    [OpenXRFeature(
        UiName = FeatureName,
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        Company = "Niantic Labs Inc.",
        Desc = "Necessary to deploy a Lightship app to a Magic Leap 2.",
        DocumentationLink = "",
        Version = "3.6.0",
        Required = false,
        Priority = -1,
        Category = FeatureCategory.Feature,
        FeatureId = FeatureID
    )]
#endif // UNITY_EDITOR

#if UNITY_ANDROID
    public class LightshipMagicLeapLoader : OpenXRFeature, ILightshipInternalLoaderSupport
    {
        private const string FeatureName = "Lightship Magic Leap Features Integration";
        public const string FeatureID = "com.nianticlabs.lightship.ml2";
        private ulong _instanceHandle;

        private LightshipLoaderHelper _lightshipLoaderHelper;
        private readonly List<ILightshipExternalLoader> _externalLoaders = new();
        private readonly List<XRCameraSubsystemDescriptor> _cameraSubsystemDescriptors = new();

#if UNITY_EDITOR
        private static class AddDefineSymbols
        {
            public static void Add(string define)
            {
                string definesString = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
                var allDefines = new HashSet<string>(definesString.Split(';'));

                if (allDefines.Contains(define))
                {
                    return;
                }

                allDefines.Add(define);
                PlayerSettings.SetScriptingDefineSymbols(
                    NamedBuildTarget.Android,
                    string.Join(";", allDefines));
            }

            public static void Remove(string define)
            {
                string definesString = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
                var allDefines = new HashSet<string>(definesString.Split(';'));
                allDefines.Remove(define);
                PlayerSettings.SetScriptingDefineSymbols(
                    NamedBuildTarget.Android,
                    string.Join(";", allDefines));
            }
        }

        [MenuItem("Lightship/Setup ML2")]
        private static void SetupML2()
        {
            // MagicLeap package-specific symbols
            AddDefineSymbols.Add("MAGICLEAP");
            AddDefineSymbols.Add("USE_ML_OPENXR");

            AddDefineSymbols.Add("NIANTIC_LIGHTSHIP_ML2_ENABLED");
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new GraphicsDeviceType[] {GraphicsDeviceType.Vulkan});

            // Apply all Project Validation fixes from LightshipMagicLeapProjectValidationRules.
            var oxrSettings = OpenXRSettings.ActiveBuildTargetInstance;
            var feature = oxrSettings.GetFeature<LightshipMagicLeapLoader>();
            feature.enabled = true;
            var mlFeature = oxrSettings.GetFeature<MagicLeapFeature>();
            mlFeature.enabled = true;
            mlFeature.EnablePerceptionSnapshots = true;
            var mlrsFeature = oxrSettings.GetFeature<MagicLeapReferenceSpacesFeature>();
            mlrsFeature.enabled = true;
        }

        [MenuItem("Lightship/Setup Non-ML2 Android")]
        private static void SetupNonML2Android()
        {
            // MagicLeap package-specific symbols
            AddDefineSymbols.Remove("MAGICLEAP");
            AddDefineSymbols.Remove("USE_ML_OPENXR");

            AddDefineSymbols.Remove("NIANTIC_LIGHTSHIP_ML2_ENABLED");
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new GraphicsDeviceType[] {GraphicsDeviceType.OpenGLES3});
        }
#endif
        protected override bool OnInstanceCreate(ulong instanceHandle)
        {
            Log.Info("[ARDK ML] Loader instance create");
            _instanceHandle = instanceHandle;
            return true;
        }

        public void InjectLightshipLoaderHelper(LightshipLoaderHelper lightshipLoaderHelper)
        {
            _lightshipLoaderHelper = lightshipLoaderHelper;
        }

        public bool InitializeWithLightshipHelper(LightshipLoaderHelper lightshipLoaderHelper)
        {
#if !UNITY_EDITOR && UNITY_ANDROID && NIANTIC_LIGHTSHIP_ML2_ENABLED
            Log.Info("[ARDK ML] Loader initialize with Lightship Helper");
            _lightshipLoaderHelper = lightshipLoaderHelper;
            return _lightshipLoaderHelper.Initialize(this);
#endif // NIANTIC_LIGHTSHIP_ML2_ENABLED
            return false;
        }

        public bool InitializePlatform()
        {
            Log.Info("[ARDK ML] Loader initialize platform");
            CreateSubsystem<XRCameraSubsystemDescriptor, XRCameraSubsystem>
            (
                _cameraSubsystemDescriptors,
                "Lightship-MLCameraSubsystem"
            );
            return base.OnInstanceCreate(_instanceHandle);
        }

        public bool DeinitializePlatform() => true;

        public bool IsPlatformDepthAvailable() => false;

        public new void CreateSubsystem<TDescriptor, TSubsystem>(List<TDescriptor> descriptors, string id)
            where TDescriptor : ISubsystemDescriptor
            where TSubsystem : ISubsystem
        {
            Log.Info("[ARDK ML] Loader creating subsystem");
            base.CreateSubsystem<TDescriptor, TSubsystem>(descriptors, id);
        }

        public new void DestroySubsystem<T>() where T : class, ISubsystem
        {
            base.DestroySubsystem<T>();
        }

        public T GetLoadedSubsystem<T>() where T : class, ISubsystem
        {
            //TODO: figure out best way to get openxrloader here (or should we make this class a XRLoaderHelper and override the subsystem creation methods for this)
            var openXRLoader = XRGeneralSettings.Instance.Manager.activeLoaders[0];
            return openXRLoader.GetLoadedSubsystem<T>();
        }

        void ILightshipLoader.AddExternalLoader(ILightshipExternalLoader loader)
        {
            _externalLoaders.Add(loader);
        }

        /// <summary>
        /// This is the equivalent to XRLoader.Initialize
        /// </summary>
        protected override void OnSubsystemCreate()
        {
            var lightshipLoaderHelper = new LightshipLoaderHelper(_externalLoaders);

            if (InitializeWithLightshipHelper(lightshipLoaderHelper) == false)
            {
                Log.Error("Could not create Lightship MagicLeap support subsystems");
            }
        }

        protected override void OnSubsystemDestroy()
        {
#if NIANTIC_LIGHTSHIP_ML2_ENABLED
            _lightshipLoaderHelper.Deinitialize();
#endif
        }

        protected override void OnInstanceDestroy(ulong instanceHandle)
        {
#if NIANTIC_LIGHTSHIP_ML2_ENABLED
            base.OnInstanceDestroy(instanceHandle);
#endif
        }

    }
#else // UNITY_ANDROID
    public class LightshipMagicLeapLoader : OpenXRFeature, ILightshipInternalLoaderSupport
    {
        private const string FeatureName = "Lightship Magic Leap Features Integration";
        public const string FeatureID = "com.nianticlabs.lightship.ml2";
        private const string ErrorMessage = "LightshipMagicLeapLoader only available on Android";

        protected override bool OnInstanceCreate(ulong instanceHandle)
        {
            throw new NotImplementedException(ErrorMessage);
        }

        protected override void OnSubsystemCreate()
        {
            throw new NotImplementedException(ErrorMessage);
        }

        protected override void OnSubsystemDestroy()
        {
            throw new NotImplementedException(ErrorMessage);
        }

        protected override void OnInstanceDestroy(ulong instanceHandle)
        {
            throw new NotImplementedException(ErrorMessage);
        }

        public void InjectLightshipLoaderHelper(LightshipLoaderHelper lightshipLoaderHelper)
        {
            throw new NotImplementedException(ErrorMessage);
        }

        public bool InitializeWithLightshipHelper(LightshipLoaderHelper lightshipLoaderHelper)
        {
            throw new NotImplementedException(ErrorMessage);
        }

        public bool InitializePlatform()
        {
            throw new NotImplementedException(ErrorMessage);
        }

        public bool DeinitializePlatform()
        {
            throw new NotImplementedException(ErrorMessage);
        }

        public bool IsPlatformDepthAvailable()
        {
            throw new NotImplementedException(ErrorMessage);
        }

        public new void CreateSubsystem<TDescriptor, TSubsystem>(List<TDescriptor> descriptors, string id) where TDescriptor : ISubsystemDescriptor where TSubsystem : ISubsystem
        {
            throw new NotImplementedException(ErrorMessage);
        }

        public new void DestroySubsystem<T>() where T : class, ISubsystem
        {
            throw new NotImplementedException(ErrorMessage);
        }

        public T GetLoadedSubsystem<T>() where T : class, ISubsystem
        {
            throw new NotImplementedException(ErrorMessage);
        }

        void ILightshipLoader.AddExternalLoader(ILightshipExternalLoader loader)
        {
            throw new NotImplementedException(ErrorMessage);
        }
    }
#endif
}
