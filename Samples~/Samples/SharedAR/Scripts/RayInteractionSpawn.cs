
#if NIANTIC_LIGHTSHIP_SHAREDAR_ENABLED

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

using ItemType = NetworkItem.NetworkItemType;

public class RayInteractionSpawn : MonoBehaviour
{
    [SerializeField]
    private XRRayInteractor _rayInteractor;

    [SerializeField]
    private GameObject _spawnDebugObject;

    [SerializeField]
    private SimpleNetworking _simpleNetworking;

    // TODO: Should probably have a different class for editor only
    [SerializeField]
    [Header("For Editor Use Only")]
    private Camera _camera;

    private bool _useNetworking;
    private MagicLeapInputs inputs;

    private void Start()
    {
#if !UNITY_EDITOR && UNITY_ANDROID && NIANTIC_LIGHTSHIP_ML2_ENABLED
        inputs = new MagicLeapInputs();
        inputs.Enable();

        inputs.Controller.Trigger.started += SpawnObjectAtLocation;
#endif
        _useNetworking = _simpleNetworking != null;
    }

    private void OnDestroy()
    {
#if !UNITY_EDITOR && UNITY_ANDROID && NIANTIC_LIGHTSHIP_ML2_ENABLED
        inputs.Controller.Trigger.started -= SpawnObjectAtLocation;
        inputs.Dispose();
#endif
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                SpawnObject(hit.point);
            }
        }
#endif
    }

#if !UNITY_EDITOR && UNITY_ANDROID && NIANTIC_LIGHTSHIP_ML2_ENABLED
    private void SpawnObjectAtLocation(InputAction.CallbackContext args)
    {
        Vector3? collisionPosition = null;
        if (args.started)
        {
            // Ignore UI hits
            if (_rayInteractor.TryGetCurrent3DRaycastHit(out var hit))
            {
                collisionPosition = hit.point;
                SpawnObject(collisionPosition);
            }
        }
    }
#endif

    private void SpawnObject(Vector3? collisionPosition)
    {
        if (!_useNetworking && collisionPosition.HasValue)
        {
            Instantiate(_spawnDebugObject, collisionPosition.Value, Quaternion.identity);
        }

        if (_useNetworking && _simpleNetworking.GameStarted && collisionPosition.HasValue)
        {
            var item = (ItemType)(Random.Range(0, (int)ItemType.Max));
            var localPos =
                _simpleNetworking.SharedSpaceOrigin.transform.InverseTransformPoint(collisionPosition.Value);
            _simpleNetworking.SpawnItem(item, localPos, Quaternion.identity);
        }
    }
}

#endif
