// Copyright 2022-2025 Niantic.
// %BANNER_BEGIN%
// ---------------------------------------------------------------------
// %COPYRIGHT_BEGIN%
// Copyright (c) (2019-2022) Magic Leap, Inc. All Rights Reserved.
// Use of this file is governed by the Software License Agreement, located here: https://www.magicleap.com/software-license-agreement-ml2
// Terms and conditions applicable to third-party materials accompanying this distribution may also be found in the top-level NOTICE file appearing herein.
// %COPYRIGHT_END%
// ---------------------------------------------------------------------
// %BANNER_END%

using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace MagicLeap.Examples
{
    [RequireComponent(typeof(PlaceFromCamera))]
    public class UserInterface : MonoBehaviour
    {
        private const float SIDE_MENU_DEFAULT_WIDTH = 175;
        private const float SIDE_MENU_MAX_WIDTH = 475;

        [Header("Settings")]
        [SerializeField, Tooltip("The default and closest distance for the canvas.")]
        private float _minDistance = 1.0f;

        [SerializeField, Tooltip("The furthest distance for the canvas.")]
        private float _maxDistance = 1.5f;

        [SerializeField, Tooltip("The primary workspace, this area will be collapsed in the minimized view.")]
        private GameObject _workspace = null;

        [SerializeField, Tooltip("Debug Metrics Panel")]
        private GameObject _debugMetricsPanel = null;

        [SerializeField, Tooltip("The button that maintains the canvas lock for the interface.")]
        private UIButton _lockButton = null;

        [Header("Interface")]
        [SerializeField, Tooltip("The transform of the side menu.")]
        private RectTransform _sideMenu = null;

        [Header("Button & Text Fields")]

        [SerializeField, Tooltip("The title text element.")]
        private Text _title = null;

        [SerializeField, Tooltip("The UIButton for the overview tab.")]
        private UIButton _overviewTab = null;

        [SerializeField]
        private Text _overviewText;

        [SerializeField]
        private Text _controlsText;

        [SerializeField, Tooltip("The UIButton for the status tab.")]
        private UIButton _statusTab = null;

        [SerializeField, Tooltip("The UIButton for the scene tab.")]
        private UIButton _SceneTab = null;

        [SerializeField, Tooltip("The UIButton for the issues tab.")]
        private UIButton _issuesTab = null;

        [SerializeField, Tooltip("The container for issue related text elements.")]
        private GameObject _issuesContent = null;

        [SerializeField, Tooltip("The text entry prefab used for multiple line entries.")]
        private GameObject _textEntryPrefab = null;

        [Header("Scene selection")]

        [SerializeField, Tooltip("Prefab of Scene Selection Button")]
        private UISceneSelectionButton sceneSelectionButtonPrefab;

        [SerializeField, Tooltip("Parent transform for all created Scene Selection Buttons")]
        private Transform scenesListTransform = null;

        [SerializeField, Tooltip("Button that opens selected scene")]
        private UIButton openSceneButton;

        [SerializeField, Tooltip("Scroll rect for scene selection buttons")]
        private ScrollRect listScrollRect;

        [Header("Example Information")]

        [SerializeField]
        private string title;

        [SerializeField, TextArea(4, 8)]
        private string overview;

        [SerializeField, TextArea(4, 8)]
        private string controls;

        private PlaceFromCamera _placeFromCamera = null;
        private float _canvasDistance = 0f;
        private string selectedScene;
        private Canvas _canvas;

        private void Awake()
        {
            // Canvas Initialization
            _placeFromCamera = GetComponent<PlaceFromCamera>();

            _canvasDistance = _minDistance;
            _placeFromCamera.Distance = _canvasDistance;

            _title.text = GetTitle();

            // Open the these two tabs by default.
            _overviewTab.Pressed();
            if (_statusTab.gameObject.activeSelf)
            {
                _statusTab.Pressed();
            }
            else
            {
                _SceneTab.Pressed();
            }
        }

        private void Start()
        {
            CreateSceneButtons();
        }

        private void OnEnable()
        {
            Application.logMessageReceived += HandleOnLogMessageReceived;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleOnLogMessageReceived;
        }

        private void OnValidate()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }

            if (_canvas != null && _canvas.worldCamera == null)
            {
                _canvas.worldCamera = Camera.main;
            }

            if (!string.IsNullOrWhiteSpace(title) && _title != null)
            {
                _title.text = title;
            }

            if (!string.IsNullOrWhiteSpace(overview) && _overviewText != null)
            {
                _overviewText.text = overview;
            }

            if (!string.IsNullOrWhiteSpace(controls) && _controlsText != null)
            {
                _controlsText.text = controls;
            }
        }

        /// <summary>
        /// Adds a new entry into the UI issues section.
        /// </summary>
        /// <param name="text">The text of the issue to add.</param>
        public void AddIssue(string text)
        {
            if (_issuesContent != null && _textEntryPrefab != null)
            {
                GameObject textEntry = Instantiate(_textEntryPrefab, _issuesContent.transform, false);
                textEntry.GetComponent<Text>().text = text;
            }
        }

        /// <summary>
        /// Clears any existing issue entries.
        /// </summary>
        public void ClearIssues()
        {
            if (_issuesContent != null)
            {
                Text[] entries = _issuesContent.GetComponentsInChildren<Text>();
                for (int i = 0; i < entries.Length; i++)
                {
                    Destroy(entries[i].gameObject);
                }
            }
        }

        /// <summary>
        /// Toggle the lock state of the canvas.
        /// </summary>
        public void ToggleCanvasLock()
        {
            _placeFromCamera.PlaceOnUpdate = !_placeFromCamera.PlaceOnUpdate;
        }

        /// <summary>
        /// Toggle the canvas distance between the min and max distance.
        /// </summary>
        public void ToggleCanvasDistance()
        {
            if (_lockButton.IsActive)
            {
                return;
            }

            _canvasDistance = (_canvasDistance == _minDistance) ? _maxDistance : _minDistance;
            _placeFromCamera.Distance = _canvasDistance;

            _placeFromCamera.ForceUpdate();
        }

        /// <summary>
        /// Toggle the visibility of the workspace.
        /// </summary>
        public void ToggleCanvas()
        {
            ShowCanvas(!_workspace.activeInHierarchy);
        }

        public void ToggleDebugMetrics()
        {
            _debugMetricsPanel.SetActive(!_debugMetricsPanel.activeInHierarchy);
        }

        /// <summary>
        /// Opens selected scene. (Invoked by UIButton)
        /// </summary>
        public void OpenScene()
        {
            LoaderUtility.Initialize();
            SceneManager.LoadScene(selectedScene);
        }

        /// <summary>
        /// Set the visibility of the workspace.
        /// </summary>
        /// <param name="visible">The desired visible state of the workspace.</param>
        public void ShowCanvas(bool visible)
        {
            _workspace.SetActive(visible);

            // Adjust the width of the side menu, this allows it to shift left/right.
            _sideMenu.sizeDelta = new Vector2((_workspace.activeInHierarchy) ? SIDE_MENU_DEFAULT_WIDTH : SIDE_MENU_MAX_WIDTH, _sideMenu.sizeDelta.y);
        }

        public void HandleOnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error)
            {
                AddIssue(FormatText(condition));

                // Only show the issues button, if an error is reported.
                StartCoroutine(SendErrorNotifications());
            }
        }

        public void QuitApplication() => Application.Quit();

        private string FormatText(string text)
        {
            if (text.Contains("Error:"))
            {
                text = text.Replace("Error:", string.Format("<color=#{0}><b>Error:</b> </color><i>", ColorUtility.ToHtmlStringRGB(Color.red))) + "</i>";
            }
            else
            {
                text = string.Format("<color=#{0}><b>Error:</b> </color><i>", ColorUtility.ToHtmlStringRGB(Color.red)) + text + "</i>";
            }

            return text;
        }

        private IEnumerator SendErrorNotifications()
        {
            yield return new WaitForEndOfFrame();

            _issuesTab.ForceActive();
        }

        private void CreateSceneButtons()
        {
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                if (i == currentSceneIndex)
                    continue;
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                UISceneSelectionButton button = Instantiate(sceneSelectionButtonPrefab, scenesListTransform);
                button.Initialize(sceneName, listScrollRect, OnSceneSelected);
                button.gameObject.name = "Scene Button - " + sceneName;
            }
        }

        private void OnSceneSelected(string sceneName)
        {
            selectedScene = sceneName;
            openSceneButton.gameObject.SetActive(true);
        }

        private string GetTitle()
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            var words =
                    Regex.Matches(SceneManager.GetActiveScene().name, @"([A-Z][a-z]+)")
                    .Cast<Match>()
                    .Select(m => m.Value);

            return string.Join(" ", words);
        }
    }
}
