// Copyright 2022-2025 Niantic.
// %BANNER_BEGIN%
// ---------------------------------------------------------------------
// %COPYRIGHT_BEGIN%
// Copyright (c) (2021-2022) Magic Leap, Inc. All Rights Reserved.
// Use of this file is governed by the Software License Agreement, located here: https://www.magicleap.com/software-license-agreement-ml2
// Terms and conditions applicable to third-party materials accompanying this distribution may also be found in the top-level NOTICE file appearing herein.
// %COPYRIGHT_END%
// ---------------------------------------------------------------------
// %BANNER_END%

using System;
using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace UnityEngine.XR.MagicLeap
{
    /// <summary>
    /// Detects the focus distance by utilizing the eye tracking fixation point either
    /// directly or in conjunction with sphere casting colliders in the scene.  If
    /// eye tracking is not used or not available, this detector will fall back to
    /// sphere casting from headpose.
    /// This component expects a MagicLeapCamera to be in the scene and will set
    /// the MagicLeapCamera.StereoConvergencePoint to control focus distance.
    /// </summary>
    public class StereoConvergenceDetector : MonoBehaviour
    {
        #region NestedType / Constructors
        [Serializable]
        public enum EyeTrackingOptions
        {
            DoNotUseEyeTracking_UseHeadpose,
            SphereCastThroughEyeFixationPoint
        }
        #endregion NestedType / Constructors

        #region Public Members
        public EyeTrackingOptions EyeTrackingOption
        {
            get => _eyeTrackingOption;
        }
        public float SphereCastInterval
        {
            set => _sphereCastInterval = value;
            get => _sphereCastInterval;
        }
        public float SphereCastRadius
        {
            set => _sphereCastRadius = value;
            get => _sphereCastRadius;
        }
        public LayerMask SphereCastMask
        {
            set => _sphereCastMask = value;
            get => _sphereCastMask;
        }
        public bool ShowDebugVisuals
        {
            get => _showDebugVisuals;
        }
        public Material SphereCastMaterial
        {
            get => _sphereCastMaterial;
        }
        public Material HitPointMaterial
        {
            get => _hitPointMaterial;
        }
        #endregion Public Members

        #region [SerializeField] Private Members
        [Header("Sphere Casting")]
        [SerializeField]
        [Tooltip("Choose if eye tracking is used at all along with how to utilize the eye fixation point.  " +
                    "Headpose vector will provide a fall back if eye tracking is not used or not available.")]
        private EyeTrackingOptions _eyeTrackingOption = EyeTrackingOptions.SphereCastThroughEyeFixationPoint;
        [SerializeField]
        [Tooltip("The interval in seconds between detecting the focus point via sphere cast or direct eye fixation point.")]
        private float _sphereCastInterval = .1f;
        [SerializeField]
        [Tooltip("The radius to use for the sphere cast when sphere casting is used.")]
        private float _sphereCastRadius = .075f;
        [SerializeField]
        [Tooltip("The layer mask for the sphere cast.")]
        private LayerMask _sphereCastMask;
        [Header("Debug Visuals")]
        [SerializeField]
        [Tooltip("Whether to show debug visuals for focus point detection.")]
        private bool _showDebugVisuals = false;
        [SerializeField]
        [Tooltip("Material representing sphere cast radius and focus point location.")]
        private Material _sphereCastMaterial;
        [SerializeField]
        [Tooltip("Material representing sphere cast hit point.")]
        private Material _hitPointMaterial;
        #endregion [SerializeField] Private Members

        #region Private Members
        private GameObject _convergencePoint = null;
        private GameObject _sphereCastVisual = null;
        private GameObject _hitPointVisual = null;
        private MagicLeapCamera _magicLeapCamera = null;
        private Coroutine _raycastRoutine = null;
        private InputDevice _eyesDevice;
        private readonly MLPermissions.Callbacks _permissionCallbacks = new MLPermissions.Callbacks();
        private XROrigin _xrOrigin = null;
        #endregion Private Members

        #region MonoBehaviour Methods
        private void Awake()
        {
            _permissionCallbacks.OnPermissionGranted += OnPermissionGranted;
            _permissionCallbacks.OnPermissionDenied += OnPermissionDenied;
            _permissionCallbacks.OnPermissionDeniedAndDontAskAgain += OnPermissionDenied;

            SetupConvergencePointObject();
        }

        private void Start()
        {
            // Request EyeTracking when an eye tracking option is selected
            if (_eyeTrackingOption != EyeTrackingOptions.DoNotUseEyeTracking_UseHeadpose)
            {
                MLPermissions.RequestPermission(MLPermission.EyeTracking, _permissionCallbacks);
            }

            // Obtain a reference to MagicLeapCamera for setting of StereoConvergencePoint.
            _magicLeapCamera = FindObjectOfType<MagicLeapCamera>();
            if (_magicLeapCamera == null)
            {
                Debug.LogWarning("No MagicLeapCamera component found, will not be able to set stereo convergence point.");
            }

            // Detect if the main camera is part of an XROrigin-based rig by obtaining the
            // XROrigin reference as a parent.
            _xrOrigin = Camera.main.GetComponentInParent<XROrigin>();
        }

        private void OnEnable()
        {
            _raycastRoutine = StartCoroutine(DetectConvergencePoint());
        }

        private void OnDisable()
        {
            if (_raycastRoutine != null)
            {
                StopCoroutine(_raycastRoutine);
                _raycastRoutine = null;
            }

            if (_showDebugVisuals)
            {
                DisplayDebugVisuals(false);
            }
        }

        private void OnDestroy()
        {
            _permissionCallbacks.OnPermissionGranted -= OnPermissionGranted;
            _permissionCallbacks.OnPermissionDenied -= OnPermissionDenied;
            _permissionCallbacks.OnPermissionDeniedAndDontAskAgain -= OnPermissionDenied;

            if (_raycastRoutine != null)
            {
                StopCoroutine(_raycastRoutine);
                _raycastRoutine = null;
            }

            if (_convergencePoint != null)
            {
                Destroy(_convergencePoint);
                _convergencePoint = null;
            }
        }
        #endregion MonoBehaviour Methods

        #region Private Methods
        private void SetupConvergencePointObject()
        {
            // Empty game object to represent the transform for the stereo convergence point
            _convergencePoint = new GameObject("Stereo Convergence Point");

            // Create visuals representing the sphere cast radius and hit point
            if (_showDebugVisuals)
            {
                Func<Material, GameObject> createSpherePrimitive = (Material material) =>
                {
                    GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    primitive.layer = this.gameObject.layer;
                    primitive.transform.SetParent(_convergencePoint.transform);
                    primitive.SetActive(false);
                    if (material != null)
                    {
                        primitive.GetComponent<Renderer>().material = material;
                    }

                    // Remove collider to not interfere with scene
                    Collider collider = primitive.GetComponent<Collider>();
                    if (collider != null)
                    {
                        Destroy(collider);
                    }

                    return primitive;
                };

                _sphereCastVisual = createSpherePrimitive(_sphereCastMaterial);
                _hitPointVisual = createSpherePrimitive(_hitPointMaterial);
            }
        }

        private IEnumerator DetectConvergencePoint()
        {
            while (true)
            {
                if (_sphereCastInterval > 0)
                {
                    yield return new WaitForSeconds(_sphereCastInterval);
                }
                else
                {
                    yield return null;
                }

                bool focusPointDetected = false;
                Vector3 focusPoint = Vector3.zero;

                // Default Headpose parameters for sphere cast
                Vector3 rayOrigin = Camera.main.transform.position;
                Vector3 rayDirection = Camera.main.transform.forward;

                // Eye Tracking option
                if (_eyeTrackingOption != EyeTrackingOptions.DoNotUseEyeTracking_UseHeadpose &&
                    MLPermissions.CheckPermission(MLPermission.EyeTracking).IsOk)
                {
                    while (!_eyesDevice.isValid)
                    {
                        _eyesDevice = InputSubsystem.Utils.FindMagicLeapDevice(InputDeviceCharacteristics.EyeTracking | InputDeviceCharacteristics.TrackedDevice);

                        yield return new WaitForSeconds(1);
                    }

                    bool hasData = _eyesDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked);
                    hasData &= _eyesDevice.TryGetFeatureValue(EyeTrackingUsages.gazePosition, out Vector3 position);
                    hasData &= _eyesDevice.TryGetFeatureValue(EyeTrackingUsages.gazeRotation, out Quaternion rotation);
                    rayDirection = rotation * Vector3.forward;
                    rayOrigin = position;
                    if (isTracked && hasData)
                    {
                        // If the camera is within an XROrigin rig, transform the fixation point
                        // into world space
                        if (_xrOrigin != null)
                        {
                            rayOrigin = _xrOrigin.CameraFloorOffsetObject.transform.TransformPoint(position);
                            rayDirection = _xrOrigin.CameraFloorOffsetObject.transform.TransformDirection(rotation * Vector3.forward);
                        }
                    }
                }

                if (Physics.SphereCast(new Ray(rayOrigin, rayDirection), _sphereCastRadius, out RaycastHit hitInfo, Camera.main.farClipPlane, _sphereCastMask))
                {
                    focusPoint = hitInfo.point;
                    focusPointDetected = true;
                }

                if (focusPointDetected)
                {
                    _convergencePoint.transform.position = focusPoint;

                    if (_magicLeapCamera != null)
                    {
                        _magicLeapCamera.StereoConvergencePoint = _convergencePoint.transform;
                    }

                    if (_showDebugVisuals)
                    {
                        DisplayDebugVisuals(true);

                        _sphereCastVisual.transform.localScale = Vector3.one * _sphereCastRadius * 2.0f;
                        _sphereCastVisual.transform.position = rayOrigin + Vector3.Project(focusPoint - rayOrigin, rayDirection);

                        _hitPointVisual.transform.localScale = Vector3.one * .02f;
                        _hitPointVisual.transform.position = focusPoint;
                    }
                }
                else
                {
                    if (_magicLeapCamera != null)
                    {
                        _magicLeapCamera.StereoConvergencePoint = null;
                    }

                    if (_showDebugVisuals)
                    {
                        DisplayDebugVisuals(false);
                    }
                }
            }
        }

        private void DisplayDebugVisuals(bool show)
        {
            if (_sphereCastVisual != null)
            {
                _sphereCastVisual.SetActive(show);
            }

            if (_hitPointVisual != null)
            {
                _hitPointVisual.SetActive(show);
            }
        }

        private void OnPermissionGranted(string permission)
        {
            InputSubsystem.Extensions.MLEyes.StartTracking();
        }

        private void OnPermissionDenied(string permission)
        {
            MLPluginLog.Error($"{permission} denied, falling back to Headpose sphere cast.");
        }
        #endregion Private Methods
    }
}
