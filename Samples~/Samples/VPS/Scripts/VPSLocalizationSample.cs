// Copyright 2022-2025 Niantic.
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.PersistentAnchors;
using Niantic.Lightship.AR.VpsCoverage;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

using Input = Niantic.Lightship.AR.Input;

namespace Niantic.Lightship.MagicLeap.InternalSamples
{
    public class VPSLocalizationSample : MonoBehaviour
    {
        [SerializeField, Tooltip("The Location Manager")]
        private ARLocationManager _arLocationManager;

        [Header("UI")]
        [SerializeField, Tooltip("The Dropdown for Persistent AR Locations")]
        private Dropdown _arLocationDropdown;

        [SerializeField, Tooltip("The Button to select an AR Location")]
        private Button _arLocationChooseButton;

        [SerializeField, Tooltip("The UI Canvas to display the AR Location Selector")]
        private GameObject _arLocationUI;

        [SerializeField]
        private Text _localizationStatusDisplayText;

        [SerializeField]
        private Text _gpsDisplayText;

        [SerializeField]
        private Text _debugDisplayText;

        private List<ARLocation> _arLocationsDropdownItems = new List<ARLocation>();

        private void OnEnable()
        {
            _debugDisplayText.text = "";
            if (LightshipSettingsHelper.ActiveSettings.LocationAndCompassDataSource != LocationDataSource.Spoof)
            {
                Debug.LogError
                    (
                        "Magic Leap does not provide GPS, which is necessary for Lightship VPS. " +
                        "Please enable the Spoof Location feature in the Lightship Settings menu in " +
                        "order to try out the VPS Localization Sample."
                    );
                _debugDisplayText.text =
                    "Error: Spoof Location is not enabled. ML2 does not provide GPS which Lightship VPS requires. Please enable Location Spoofing in Lightship Settings.";

                return;
            }

            _arLocationManager.locationTrackingStateChanged += OnLocationTrackingStateChanged;
            _arLocationManager.subsystem.debugInfoProvided += OnDebugInfoProvided;

            if (_arLocationManager.AutoTrack)
            {
                _arLocationUI.SetActive(false);
                _arLocationChooseButton.GetComponentInChildren<Text>().text = "Automatic Localization";
            }
            else
            {
                CreateARLocationMenu();
                _arLocationUI.SetActive(true);
                _arLocationChooseButton.onClick.AddListener(OnLocationSelected);
            }

            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                Input.location.Start();
            }

            _localizationStatusDisplayText.text = "Status: Stopped";
            UpdateGPSText();
        }

        private void OnDebugInfoProvided(XRPersistentAnchorDebugInfo info)
        {
            var errors = info.networkStatusArray.Where(s => s.Status == RequestStatus.Failed).Select(s => s.Error).ToArray();
            if (errors.Length > 0)
            {
                _debugDisplayText.text = "Network errors: " + string.Join(",", errors) + "\nPlease ensure you are connected to WIFI and have set a valid API key in Lightship Settings.";
            }
        }

        private void OnDisable()
        {
            _arLocationManager.locationTrackingStateChanged -= OnLocationTrackingStateChanged;
            _arLocationChooseButton.onClick.RemoveAllListeners();
        }

        private void CreateARLocationMenu()
        {
            var arLocations = _arLocationManager.ARLocations;
            foreach (var arLocation in arLocations)
            {
                _arLocationDropdown.options.Add(new Dropdown.OptionData(arLocation.name));
                _arLocationsDropdownItems.Add(arLocation);
            }

            if (_arLocationsDropdownItems.Count > 0)
            {
                _arLocationChooseButton.interactable = true;
            }
        }

        private void OnLocationSelected()
        {
            var currentIndex = _arLocationDropdown.value;
            var arLocation = _arLocationsDropdownItems[currentIndex];
            _arLocationManager.SetARLocations(arLocation);

            var gpsLocation = arLocation.GpsLocation;
            var locationInfo = LightshipSettingsHelper.ActiveSettings.SpoofLocationInfo;
            locationInfo.Latitude = (float)gpsLocation.Latitude;
            locationInfo.Longitude = (float)gpsLocation.Longitude;
            UpdateGPSText();
            _localizationStatusDisplayText.text = "Status: Trying to localize...";

            _arLocationManager.StartTracking();
            _arLocationChooseButton.onClick.RemoveAllListeners();
            _arLocationChooseButton.onClick.AddListener(OnStopTracking);
            _arLocationChooseButton.GetComponentInChildren<Text>().text = "Stop Localization";
        }

        private void OnStopTracking()
        {
            _arLocationManager.StopTracking();
            _arLocationChooseButton.onClick.RemoveAllListeners();
            _arLocationChooseButton.onClick.AddListener(OnLocationSelected);
            _localizationStatusDisplayText.text = "Status: Stopped";
            _arLocationChooseButton.GetComponentInChildren<Text>().text = "Start Localization";
        }

        private void OnLocationTrackingStateChanged(ARLocationTrackedEventArgs args)
        {
            _localizationStatusDisplayText.text =
                $"Status: {(args.Tracking ? "Tracking Success" : $"Tracking Failure (reason: {args.TrackingStateReason})")}";

            args.ARLocation.gameObject.SetActive(args.Tracking);
        }

        private void UpdateGPSText()
        {
            // First, check if user has location service enabled
            if (!Input.location.isEnabledByUser)
            {
                _gpsDisplayText.text = "Location service is not enabled";
                return;
            }

            var latitude = Input.location.lastData.latitude;
            var longitude = Input.location.lastData.longitude;
            var altitude = Input.location.lastData.altitude;

            _gpsDisplayText.text = $"Latitude: {latitude}\nLongitude: {longitude}\nAltitude: {altitude}";
        }
    }
}
