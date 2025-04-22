// Copyright 2022-2025 Niantic.

#if NIANTIC_LIGHTSHIP_SHAREDAR_ENABLED

using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Loader;
using UnityEngine;
using Niantic.Lightship.SharedAR.Colocalization;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.SharedAR.Networking;
using Unity.XR.CoreUtils;
using UnityEngine.UI;

using Input = Niantic.Lightship.AR.Input;

namespace Niantic.Lightship.MagicLeap.InternalSamples
{
    public class MagicLeapColocalization : MonoBehaviour
    {
        [SerializeField]
        private SharedSpaceManager _sharedSpaceManager;

        [SerializeField]
        private SimpleNetworking _simpleNetworking;

        [SerializeField]
        private XROrigin _xrOrigin;

        [SerializeField]
        private GameObject _sharedRootMarkerPrefab;

        [SerializeField]
        private Text _roomNameDisplayText;

        [SerializeField]
        private Text _locationInfoText;

        [SerializeField]
        private Text _multiplayerText;

        private ARLocation _arLocation;

        public ARLocation ArLocation
        {
            get { return _arLocation; }
            set { _arLocation = value; }
        }

        // private string _deviceId;
        private PeerID _peerId;
        private string _roomName;
        private bool _startAsHost;

        private void Start()
        {
            Debug.Log("Starting VPS Colocalization");
        }

        private void OnDestroy()
        {
            _sharedSpaceManager.sharedSpaceManagerStateChanged -= OnColocalizationTrackingStateChanged;
        }

        private void OnColocalizationTrackingStateChanged(
            SharedSpaceManager.SharedSpaceManagerStateChangeEventArgs args)
        {
            string localizationStatus;
            if (args.Tracking)
            {
                Debug.Log("Colocalized.");
                localizationStatus = "Localized";

                // create an origin marker object and set under the sharedAR origin
                Instantiate(_sharedRootMarkerPrefab,
                    _sharedSpaceManager.SharedArOriginObject.transform, false);

                var networking = _sharedSpaceManager.SharedSpaceRoomOptions.Room.Networking as LightshipNetworking;
                _simpleNetworking.OnGameStart(_sharedSpaceManager.SharedArOriginObject, networking);
            }
            else
            {
                Debug.Log($"Not VPS Tracking");
                localizationStatus = "Not Tracking";
            }

            var locationInfoString =
                $"Location: {_arLocation.name}\nLatitude: {Input.location.lastData.latitude}\nLongitude: {Input.location.lastData.longitude}\nStatus: {localizationStatus}";
            _locationInfoText.text = locationInfoString;
        }

        private void JoinRoom()
        {
            // Let the SharedSpaceManager handle the colocalization tracking state change
            _sharedSpaceManager.sharedSpaceManagerStateChanged += OnColocalizationTrackingStateChanged;

            var vpsTrackingArgs = ISharedSpaceTrackingOptions.CreateVpsTrackingOptions(_arLocation);

#if UNITY_EDITOR
            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                Input.location.Start();
            }
#endif

            if (!Input.location.isEnabledByUser)
            {
                _locationInfoText.text = "Location service is not enabled";
            }
            else
            {
                var localizationStatus = "Trying to Localize";
                var locationInfoString =
                    $"Location: {_arLocation.name}\nLatitude: {Input.location.lastData.latitude}\nLongitude: {Input.location.lastData.longitude}\nStatus: {localizationStatus}";
                _locationInfoText.text = locationInfoString;
            }

            var roomArgs = SetupRoomAndUI(vpsTrackingArgs);
            Debug.Log("Room name: " + _roomName);

            _sharedSpaceManager.StartSharedSpace(vpsTrackingArgs, roomArgs);
            _sharedSpaceManager.SharedSpaceRoomOptions.Room.Initialize();

            _sharedSpaceManager.SharedSpaceRoomOptions.Room.Networking.NetworkEvent += OnNetworkEvent;
            _sharedSpaceManager.SharedSpaceRoomOptions.Room.Join();
        }

        public void StartNewRoom()
        {
            // start as host
            _startAsHost = true;

            // generate a new room name. 3 digit number.
            int code = (int)Random.Range(0.0f, 999.0f);
            _roomName = code.ToString("D3");

            JoinRoom();
        }

        public void Join(string roomNameInput)
        {
            // start as client
            _startAsHost = false;

            // set room name from text box
            _roomName = roomNameInput;

            JoinRoom();
        }

        private void OnNetworkEvent(NetworkEventArgs args)
        {
            if (args.networkEvent == NetworkEvents.Disconnected || args.networkEvent == NetworkEvents.ArdkShutdown)
            {
                return;
            }

            _peerId = _sharedSpaceManager.SharedSpaceRoomOptions.Room.Networking.SelfPeerID;
            Debug.Log("Peer ID: " + _peerId);
            Debug.Log("Number of peers: " + _sharedSpaceManager.SharedSpaceRoomOptions.Room.Networking.PeerIDs.Count);
            Debug.Log(_sharedSpaceManager.SharedSpaceRoomOptions.Room.RoomParams.Name);

            _multiplayerText.text =
                $"Peer ID: {_peerId}\nNumber of Peers: {_sharedSpaceManager.SharedSpaceRoomOptions.Room.Networking.PeerIDs.Count}";

            _sharedSpaceManager.SharedSpaceRoomOptions.Room.Networking.NetworkEvent -= OnNetworkEvent;
        }

        private ISharedSpaceRoomOptions SetupRoomAndUI(ISharedSpaceTrackingOptions trackingVpsLocation)
        {
            // update UI
            _roomNameDisplayText.text = $"PIN: {_roomName}";
            _roomNameDisplayText.gameObject.SetActive(true);

            // Create a room args and return it
            return ISharedSpaceRoomOptions.CreateVpsRoomOptions(
                trackingVpsLocation,
                _roomName,
                32,
                "vps tracking colocalization demo",
                false
            );

        }
    }
}

#endif
