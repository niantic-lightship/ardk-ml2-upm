using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace Niantic.Lightship.MagicLeap.Samples
{
    public class BallLauncher : MonoBehaviour
    {
        [SerializeField]
        private Rigidbody _prefabWithRigidbody;

        [SerializeField]
        private TrackedPoseDriver _trackedPoseDriver;

        private Camera _camera;
        private MagicLeapController _controller;

        private IEnumerator Start()
        {
            _camera = Camera.main;

#if !UNITY_EDITOR
            // Wait for the MagicLeapController to be initialized
            const float timeout = 5.0f;
            var startTime = Time.time;
            while (MagicLeapController.Instance == null && Time.time - startTime < timeout)
            {
                yield return null;
            }

            if (MagicLeapController.Instance == null)
            {
                Debug.LogError("MagicLeapController not initialized after timeout");
                Destroy(this);
                yield break;
            }

            // Register for the trigger pressed event
            _controller = MagicLeapController.Instance;
            _controller.TriggerPressed += TriggerPressedEvent;
#endif

            yield break;
        }

        private void OnDestroy()
        {
#if !UNITY_EDITOR
            // Unregister for the trigger pressed event
            if (_controller != null)
            {
                _controller.TriggerPressed -= TriggerPressedEvent;
            }
#endif
        }

#if UNITY_EDITOR
        void Update()
        {
            if (InputSystem.GetDevice<Mouse>().rightButton.wasReleasedThisFrame)
            {
                // spawn in front of at the camera
                var cameraPos = _camera.transform.position;
                var forw = _camera.transform.forward;
                SpawnObjectWithForce(cameraPos, forw);
            }
        }
#endif

        private void TriggerPressedEvent(InputAction.CallbackContext args)
        {
            var controllerPos = _trackedPoseDriver.transform.position;
            var controllerForw = _trackedPoseDriver.transform.forward;
            SpawnObjectWithForce(controllerPos, controllerForw);
        }

        private void SpawnObjectWithForce(Vector3 startPos, Vector3 forceDirection)
        {
            // spawn in front of at the camera
            var thing = Instantiate(_prefabWithRigidbody, startPos + (forceDirection * 0.4f), Quaternion.identity);

            thing.AddForce(forceDirection * 200.0f);
        }
    }
}
