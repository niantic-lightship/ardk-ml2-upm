// Copyright 2022-2025 Niantic.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Niantic.ARDK.AR.Scanning;
using Niantic.Lightship.AR.Scanning;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;

namespace Niantic.Lightship.AR.Samples
{
    public class RecordingDemo : MonoBehaviour
    {
        [Tooltip("The manager used to perform the scanning")]
        [SerializeField]
        private ARScanningManager _arScanningManager;

        [Tooltip("Export Panel")]
        [SerializeField]
        private ExportScanPanel _exportScanPanel;

        [Tooltip("Button to start scanning")]
        [SerializeField]
        private Button _startScanningButton;

        [Tooltip("Slider to set framerate")]
        [SerializeField]
        private Slider _framerateSlider;

        [Tooltip("Text of current framerate")]
        [SerializeField]
        private Text _framerateText;

        [Tooltip("Slider to set max recording time per file")]
        [SerializeField]
        private Slider _maxTimePerChunkSlider;

        [Tooltip("Text of current max recording time per file")]
        [SerializeField]
        private Text _maxTimePerChunkText;

        [Tooltip("Button to stop scanning")]
        [SerializeField]
        private Button _stopScanningButton;

        [Tooltip("Button to export a scan")]
        [SerializeField]
        private Button _startExportButton;

        [SerializeField]
        private ScanValidator _scanValidator;

        private ScanStore _scanStore;
        private ScanStore.SavedScan _savedScan;

        private void Start()
        {
            OnFramerateChange();
            OnChunkTimeChange();
            _scanStore = _arScanningManager.GetScanStore();
            InitializeLocation();
            _startScanningButton.onClick.AddListener(StartScanning);
            _stopScanningButton.onClick.AddListener(StopScanning);
            _startExportButton.onClick.AddListener(StartExporting);
        }

        private async void InitializeLocation()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                var androidPermissionCallbacks = new PermissionCallbacks();
                androidPermissionCallbacks.PermissionGranted += permissionName =>
                {
                    if (permissionName == "android.permission.ACCESS_FINE_LOCATION")
                    {
                        InitializeLocation();
                    }
                };

                Permission.RequestUserPermission(Permission.FineLocation, androidPermissionCallbacks);
                return;
            }
#endif
            Input.compass.enabled = true;
            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                Input.location.Start();
                while (Input.location.status != LocationServiceStatus.Running)
                {
                    await Task.Delay(100); // ms
                }
            }
        }

        public void StartScanning()
        {
            _stopScanningButton.gameObject.SetActive(true);
            _startExportButton.gameObject.SetActive(false);
            _startScanningButton.gameObject.SetActive(false);
            _framerateSlider.gameObject.SetActive(false);
            _maxTimePerChunkSlider.gameObject.SetActive(false);
            _arScanningManager.ScanRecordingFramerate = (int)_framerateSlider.value;
            _arScanningManager.enabled = true;
            _scanValidator.ScanStarted();
        }

        public async void StopScanning()
        {
            _stopScanningButton.gameObject.SetActive(false);
            await _arScanningManager.SaveScan();
            _arScanningManager.enabled = false;
            _startExportButton.gameObject.SetActive(true);
            _framerateSlider.gameObject.SetActive(true);
            _maxTimePerChunkSlider.gameObject.SetActive(true);
            string scanID = _arScanningManager.GetCurrentScanId();
            _savedScan = _scanStore.GetSavedScans().First(s => s.ScanId == scanID);
            _scanValidator.ScanStopped();

        }

        public async void StartExporting()
        {
            _exportScanPanel.gameObject.SetActive(true);
            int maxFramesPerChunk = (int)(_maxTimePerChunkSlider.value * _framerateSlider.value);
            using var exportPayloadBuilder = new ScanArchiveBuilder(_savedScan, new UploadUserInfo(), maxFramesPerChunk);
            _exportScanPanel.SetExportStatusText(true, "");
            string message = "";
            string validationMessage = "";
            while (exportPayloadBuilder.HasMoreChunks())
            {
                var exportTask = exportPayloadBuilder.CreateTaskToGetNextChunk();
                exportTask.Start();
                string chunk = await exportTask;
                message += chunk;
                message += "\n";
                _exportScanPanel.SetExportStatusText(true, message);
                validationMessage += _scanValidator.ValidatedScan(_savedScan, chunk);
                validationMessage += "\n";
            }
            _exportScanPanel.SetExportStatusText(false, message);
            _exportScanPanel.SetValidationText(validationMessage);
        }

        public void OnFramerateChange()
        {
            _framerateText.text = "Framerate: " + _framerateSlider.value;
        }

        public void OnChunkTimeChange()
        {
            _maxTimePerChunkText.text = "Chunk Time: " + _maxTimePerChunkSlider.value + "s";
        }
    }
}
