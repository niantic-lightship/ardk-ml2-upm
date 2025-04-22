// Copyright 2022-2025 Niantic.

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.MagicLeap.Samples
{
    public class MeshDisplayToggler : MonoBehaviour
    {
        [SerializeField]
        private ARMeshManager _arMeshManager;

        [SerializeField]
        private Toggle _displayToggle;

        [SerializeField]
        private MeshFilter _visibleMeshPrefab;

        [SerializeField]
        private MeshFilter _hiddenMeshPrefab;


        private void OnEnable()
        {
            _displayToggle.onValueChanged.AddListener(ToggleMeshDisplay);
        }

        private void OnDisable()
        {
            _displayToggle.onValueChanged.AddListener(ToggleMeshDisplay);
        }

        private void ToggleMeshDisplay(bool isEnabled)
        {
            _arMeshManager.meshPrefab = isEnabled ? _visibleMeshPrefab : _hiddenMeshPrefab;

            // This will destroy all the spawned mesh filters but not the actual mesh data.
            // Thus, you need to look at an area again to restore the mesh (bad), but the
            // mesh will be immediately restored instead of created anew (good).
            _arMeshManager.DestroyAllMeshes();
        }
    }
}
