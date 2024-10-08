
#if NIANTIC_LIGHTSHIP_SHAREDAR_ENABLED

using System.Collections.Generic;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.VpsCoverage;
using UnityEngine;
using UnityEngine.UI;
using Input = UnityEngine.Input;

namespace Niantic.Lightship.MagicLeap.InternalSamples
{
    public class MultiplayerUI : MonoBehaviour
    {
        [SerializeField, Tooltip("The Location Manager")]
        private ARLocationManager _arLocationManager;

        [SerializeField, Tooltip("The Dropdown for Persistent AR Locations")]
        private Dropdown _arLocationDropdown;

        [SerializeField, Tooltip("The Button to choose AR Location")]
        private Button _arLocationChooseButton;

        [SerializeField]
        private Button _hostButton;

        [SerializeField]
        private Button _joinButton;

        [SerializeField]
        private NumpadPanel _numpadPanel;

        [SerializeField]
        private MagicLeapColocalization _magicLeapColocalization;

        private List<ARLocation> _arLocations = new List<ARLocation>();

        private void OnEnable()
        {
            _arLocationChooseButton.interactable = false;
            _hostButton.interactable = false;
            _joinButton.interactable = false;

            _arLocationChooseButton.onClick.AddListener(OnChooseLocation);
            _hostButton.onClick.AddListener(_magicLeapColocalization.StartNewRoom);
            _joinButton.onClick.AddListener(JoinRoom);

            _numpadPanel.OnNumberEntered += TryTurnOnJoinButton;

#if UNITY_EDITOR
            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                Input.location.Start();
            }
#endif

            CreateARLocationMenu();
        }

        private void OnDisable()
        {
            _arLocationChooseButton.onClick.RemoveListener(OnChooseLocation);
            _hostButton.onClick.RemoveListener(_magicLeapColocalization.StartNewRoom);
            _joinButton.onClick.RemoveListener(JoinRoom);

            _numpadPanel.OnNumberEntered -= TryTurnOnJoinButton;
        }

        private void CreateARLocationMenu()
        {
            var arLocations = _arLocationManager.ARLocations;
            foreach (var arLocation in arLocations)
            {
                _arLocationDropdown.options.Add(new Dropdown.OptionData(arLocation.name));
                _arLocations.Add(arLocation);
            }

            if (_arLocations.Count > 0)
            {
                _arLocationChooseButton.interactable = true;
            }
        }

        private void OnChooseLocation()
        {
            var currentIndex = _arLocationDropdown.value;
            var arLocation = _arLocations[currentIndex];
            _arLocationManager.SetARLocations(arLocation);
            LatLng gpsLocation = arLocation.GpsLocation;

            if (LightshipSettings.Instance.UseLightshipSpoofLocation)
            {
                LightshipLocationSpoof.Instance.Latitude = (float)gpsLocation.Latitude;
                LightshipLocationSpoof.Instance.Longitude = (float)gpsLocation.Longitude;
            }

            _magicLeapColocalization.ArLocation = arLocation;

            TryTurnOnJoinButton();
        }

        private void TryTurnOnJoinButton()
        {
            if (_numpadPanel == null)
            {
                Debug.LogWarning("NumpadPanel is not set. Cannot turn on Join button.");
                return;
            }

            if (_numpadPanel.CurrentNumber.Length > 0 && _magicLeapColocalization.ArLocation != null)
            {
                _joinButton.interactable = true;
                _hostButton.interactable = false;
            }
            else
            {
                _hostButton.interactable = (_magicLeapColocalization.ArLocation != null);
                _joinButton.interactable = false;
            }
        }

        private void JoinRoom()
        {
            _magicLeapColocalization.Join(_numpadPanel.CurrentNumber);
        }
    }
}

#endif
