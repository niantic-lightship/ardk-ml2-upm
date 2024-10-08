using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.MagicLeap.Samples
{
    public class DepthControls : MonoBehaviour
    {
        [Header("Necessary Components")]
        [SerializeField]
        private ARCameraBackground _cameraBackground;

        [Header("Camera Background Options")]
        [SerializeField]
        private bool _useCameraShaderToggle;

        [SerializeField]
        private Material _cameraShaderMaterial;

        [SerializeField]
        private Toggle _cameraShaderToggle;

        [Header("Occludee Options")]
        [SerializeField]
        private bool _useObjectDistanceSlider;

        [SerializeField]
        private Slider _objectDistanceSlider;

        [SerializeField]
        private Text _objectDistanceText;

        [SerializeField]
        private float _minObjectDistance = 0.25f;

        [SerializeField]
        private float _maxObjectDistance = 15.0f;

        [SerializeField]
        private GameObject _occludeeObject;

        private const string ObjectDistanceTextFormat = "Occludee Distance: {0} m";

        private void OnEnable()
        {
            // Camera Shader Settings
            if (_useCameraShaderToggle)
            {
                if (_cameraShaderToggle == null)
                {
                    Debug.LogError("Camera Shader Toggle is not set.");
                }
                else if (_cameraShaderMaterial == null)
                {
                    Debug.LogError("Camera Shader Material is not set.");
                    _cameraShaderToggle.interactable = false;
                }
                else if (_cameraBackground == null)
                {
                    Debug.LogError("Camera Background is not set.");
                    _cameraShaderToggle.interactable = false;
                }
                else
                {
                    _cameraShaderToggle.onValueChanged.AddListener(OnCameraShaderToggleValueChanged);
                }
            }

            // Occludee Object Settings
            if (_useObjectDistanceSlider)
            {
                if (_objectDistanceSlider == null)
                {
                    Debug.LogError("Object Distance Slider is not set.");
                }
                else if (_occludeeObject == null)
                {
                    Debug.LogError("Occludee Object is not set.");
                    _objectDistanceSlider.interactable = false;
                }
                else
                {
                    _objectDistanceSlider.minValue = _minObjectDistance;
                    _objectDistanceSlider.maxValue = _maxObjectDistance;
                    _objectDistanceSlider.onValueChanged.AddListener(OnObjectDistanceSliderValueChanged);
                    _objectDistanceSlider.value = _occludeeObject.transform.localPosition.z;
                }
            }
        }

        private void OnDisable()
        {
            if (_useCameraShaderToggle)
            {
                _cameraShaderToggle.onValueChanged.RemoveListener(OnCameraShaderToggleValueChanged);
            }

            if (_useObjectDistanceSlider)
            {
                _objectDistanceSlider.onValueChanged.RemoveListener(OnObjectDistanceSliderValueChanged);
            }
        }

        private void OnCameraShaderToggleValueChanged(bool value)
        {
            if (_cameraShaderMaterial != null)
            {
                _cameraBackground.useCustomMaterial = value;
                _cameraBackground.customMaterial = value ? _cameraShaderMaterial : null;
            }
        }

        private void OnObjectDistanceSliderValueChanged(float value)
        {
            if (_occludeeObject != null) // Ensure the occludee object is present
            {
                _occludeeObject.transform.localPosition = new Vector3(0, 0, value);
                _objectDistanceText.text = String.Format(ObjectDistanceTextFormat, value.ToString("F2"));
            }
        }
    }
}
