using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using MagicLeap.OpenXR.Features;

public class SetCustomFocusDistance : MonoBehaviour
{
    public Transform FocusTarget;

    private Camera camera;
    private MagicLeapRenderingExtensionsFeature renderFeature;

    private void Start()
    {
        camera = Camera.main;
        renderFeature = OpenXRSettings.Instance.GetFeature<MagicLeapRenderingExtensionsFeature>();
        if (camera == null || renderFeature == null || renderFeature.enabled == false)
        {
            Debug.LogError("Focus Distance cannot be set. Disabling script. " +
                           "Ensure all requirements are met : \n" +
                           $"Camera is present : {camera !=null} \n" +
                           $"Render Feature is present : {renderFeature != null} \n" +
                           $"Render Feature is enabled : {renderFeature.enabled}");
            enabled = false;
        }
    }

    void LateUpdate()
    {
        camera.stereoConvergence = CalculateFocusDistance();

        if (renderFeature == null)
            return;

        renderFeature.FocusDistance = camera.stereoConvergence;
    }

    private float CalculateFocusDistance()
    {
        // Get Focus Distance and log warnings if not within the allowed value bounds.
        float focusDistance = camera.stereoConvergence;
        Debug.LogWarning($"qaqaqa focusDistance = {focusDistance}");
        if (FocusTarget != null)
        {
            // From Unity documentation:
            // Note that camera space matches OpenGL convention: camera's forward is the negative Z axis.
            // This is different from Unity's convention, where forward is the positive Z axis.
            Vector3 worldForward = new Vector3(0.0f, 0.0f, -1.0f);
            Vector3 camForward = camera.cameraToWorldMatrix.MultiplyVector(worldForward);
            camForward = camForward.normalized;

            // We are only interested in the focus object's distance to the camera forward tangent plane.
            focusDistance = Vector3.Dot(FocusTarget.position - transform.position, camForward);
        }

        float nearClip = camera.nearClipPlane;
        if (focusDistance < nearClip)
        {
            focusDistance = nearClip;
        }

        return focusDistance;
    }
}


