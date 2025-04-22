// Copyright 2022-2025 Niantic.
using Niantic.Lightship.AR.Occlusion;
using Niantic.Lightship.AR.Semantics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.MagicLeap.Samples
{
    public class OcclusionControls : MonoBehaviour
    {
        [SerializeField]
        private Transform _cameraParent;

        [SerializeField]
        private MeshFilter _meshPrefab;

        [SerializeField]
        private LightshipOcclusionExtension _occlusion;

        [SerializeField]
        private Toggle _visualizeOcclusionToggle;

        [SerializeField]
        private Toggle _suppressionOcclusionToggle;

        private void OnEnable()
        {
            _visualizeOcclusionToggle.onValueChanged.AddListener(VisualizeOcclusionToggle_ValueChanged);
            _suppressionOcclusionToggle.onValueChanged.AddListener(SuppressionToggle_ValueChanged);
        }

        private void OnDisable()
        {
            _visualizeOcclusionToggle.onValueChanged.RemoveListener(VisualizeOcclusionToggle_ValueChanged);
            _suppressionOcclusionToggle.onValueChanged.RemoveListener(SuppressionToggle_ValueChanged);
        }

        private void VisualizeOcclusionToggle_ValueChanged(bool isOn)
        {
            // Try to enable the feature
            _occlusion.Visualization = isOn;

            // Update the toggle
            _visualizeOcclusionToggle.isOn = _occlusion.Visualization;
        }

        private void SuppressionToggle_ValueChanged(bool isOn)
        {
            // Add dependencies
            if (isOn)
            {
                EnsureSemanticSegmentationManager();
            }

            // Try to enable the feature
            _occlusion.IsOcclusionSuppressionEnabled = isOn;

            if (_occlusion.IsOcclusionSuppressionEnabled)
            {
                // Configure the feature
                _occlusion.AddSemanticSuppressionChannel("ground");
                _occlusion.AddSemanticSuppressionChannel("artificial_ground");

                // Update the toggle
                _suppressionOcclusionToggle.isOn = _occlusion.IsOcclusionSuppressionEnabled;
            }
        }

        private void EnsureSemanticSegmentationManager()
        {
            if (FindObjectOfType<ARSemanticSegmentationManager>() == null)
            {
                var semanticManager = new GameObject("ARSemanticSegmentationManager");
                semanticManager.transform.SetParent(_cameraParent);
                semanticManager.AddComponent<ARSemanticSegmentationManager>();
            }
        }
    }
}
