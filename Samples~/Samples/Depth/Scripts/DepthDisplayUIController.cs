using Niantic.Lightship.AR.Loader;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.Lightship.MagicLeap.Samples
{
    public class DepthDisplayUIController : MonoBehaviour
    {
        [SerializeField]
        private DepthWithConfidenceDisplay _depthWithConfidenceDisplay;

        [SerializeField]
        private Toggle _toggleConfidenceImage;

        [SerializeField]
        private Slider _sliderConfidenceThreshold;

        [SerializeField]
        private Text _sliderConfidenceThresholdText;

        private void OnEnable()
        {
            _toggleConfidenceImage.isOn = _depthWithConfidenceDisplay.UseConfidenceImage;

            // For now, on the Magic Leap platform we are co-opting this setting to mean "Prefer Magic Leap depth"
            _toggleConfidenceImage.interactable = LightshipSettingsHelper.ActiveSettings.PreferLidarIfAvailable;

            _sliderConfidenceThreshold.value = _depthWithConfidenceDisplay.ConfidenceThreshold;
            _sliderConfidenceThreshold.interactable = _depthWithConfidenceDisplay.UseConfidenceImage;
            _sliderConfidenceThresholdText.text =
                $"Confidence Value: {_depthWithConfidenceDisplay.ConfidenceThreshold}";

            _toggleConfidenceImage.onValueChanged.AddListener(OnToggleConfidenceImageValueChanged);
            _sliderConfidenceThreshold.onValueChanged.AddListener(OnSliderConfidenceThresholdValueChanged);
        }

        private void OnDisable()
        {
            _toggleConfidenceImage.onValueChanged.RemoveListener(OnToggleConfidenceImageValueChanged);
            _sliderConfidenceThreshold.onValueChanged.RemoveListener(OnSliderConfidenceThresholdValueChanged);
        }

        private void OnToggleConfidenceImageValueChanged(bool value)
        {
            _depthWithConfidenceDisplay.UseConfidenceImage = value;
            _sliderConfidenceThreshold.interactable = value;
        }

        private void OnSliderConfidenceThresholdValueChanged(float value)
        {
            _depthWithConfidenceDisplay.ConfidenceThreshold = value;
            _sliderConfidenceThresholdText.text = $"Confidence Value: {value}";
        }
    }
}
