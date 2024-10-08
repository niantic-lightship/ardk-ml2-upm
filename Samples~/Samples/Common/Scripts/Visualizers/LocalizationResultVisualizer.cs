using System.Collections.Generic;

using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.PersistentAnchors;

using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems
{
    public class LocalizationResultVisualizer :
        MonoBehaviour
    {
        [SerializeField]
        private ARLocationManager ARLocationManager;

        [SerializeField]
        private GameObject gizmo;

        private GameObject _parent;
        private List<Pose> _poses = new List<Pose>();
        private LineRenderer LocalizationRenderer;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        void Start()
        {
            if (!ARLocationManager)
            {
                ARLocationManager = FindObjectOfType<ARLocationManager>();
                if (!ARLocationManager)
                {
                    Debug.LogError("Could not find ARLocationManager");
                    Destroy(this);
                }
            }

            _parent = new GameObject();
            ARLocationManager.locationTrackingStateChanged += OnLocationTrackingStateChanged;
            LocalizationRenderer = _parent.AddComponent<LineRenderer>();
            LocalizationRenderer.startWidth = 0.1f;
            LocalizationRenderer.material = new Material(Shader.Find("Sprites/Default"));
            LocalizationRenderer.widthMultiplier = 0.1f;

            // A simple 2 color gradient with a fixed alpha of 1.0f.
            float alpha = 1.0f;
            Gradient gradient = new Gradient();
            gradient.SetKeys
            (
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.blue, 0.0f), new GradientColorKey(Color.yellow, 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(alpha, 0.0f), new GradientAlphaKey(alpha, 1.0f)
                }
            );

            LocalizationRenderer.colorGradient = gradient;
        }

        private void OnDisable()
        {
            ARLocationManager.locationTrackingStateChanged -= OnLocationTrackingStateChanged;
        }

        private void OnLocationTrackingStateChanged(ARLocationTrackedEventArgs args)
        {
            if (args.Tracking)
            {
                var pose = new Pose
                    (args.ARLocation.transform.position, args.ARLocation.transform.rotation);

                _poses.Add(pose);
                LocalizationRenderer.positionCount = _poses.Count;
                LocalizationRenderer.SetPosition(_poses.Count - 1, pose.position);

                var giz = Instantiate(gizmo, args.ARLocation.transform, false);

                // Unparent the gizmo so it doesn't get moved when the ARLocation moves.
                giz.transform.parent = this.gameObject.transform;
                giz.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                _spawned.Add(giz);
                ComputeAndDisplayVariance();
            }
        }

        private void ComputeAndDisplayVariance()
        {
            var avg = Vector3.zero;
            var pairwiseDistance = 0.0f;

            for (var i = 0; i < _poses.Count - 1; i++)
            {
                pairwiseDistance += (_poses[i].position - _poses[i + 1].position).magnitude /
                    (_poses.Count - 1);
            }

            foreach (var pose in _poses)
            {
                avg += pose.position / _poses.Count;
            }

            var variance = 0f;
            foreach (var pose in _poses)
            {
                variance += (pose.position - avg).magnitude / _poses.Count;
            }

            Debug.Log($"For {_poses.Count} poses, the variance is {variance} and the average interpolation distance is {pairwiseDistance}");
        }

        private void OnDestroy()
        {
            if (ARLocationManager)
            {
                ARLocationManager.locationTrackingStateChanged -= OnLocationTrackingStateChanged;
            }

            foreach (var obj in _spawned)
            {
                Destroy(obj);
            }

            if (_parent)
            {
                Destroy(_parent);
            }
        }
    }
}
