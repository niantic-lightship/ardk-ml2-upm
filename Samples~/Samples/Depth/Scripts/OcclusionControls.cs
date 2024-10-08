using Niantic.Lightship.AR.Occlusion;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.Lightship.MagicLeap.Samples
{
    public class OcclusionControls : MonoBehaviour
    {
        [SerializeField]
        private LightshipOcclusionEffect _occlusion;

        [SerializeField]
        private Toggle _visualizeOcclusionToggle;

        private void OnEnable()
        {
            _visualizeOcclusionToggle.onValueChanged.AddListener(OnVisualizeOcclusionToggleValueChanged);
        }

        private void OnDisable()
        {
            _visualizeOcclusionToggle.onValueChanged.RemoveListener(OnVisualizeOcclusionToggleValueChanged);
        }

        private void OnVisualizeOcclusionToggleValueChanged(bool args)
        {
            _occlusion.DebugVisualization = args;
        }
    }
}
