using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.MagicLeap;

public static class MagicLeapCameraUtilities
{
    // The length to be used for the ray when no intersection with the WorldMesh is found.
    // This prevents the ray from "popping" or suddenly changing length visually.
    private static float s_rayLength = 10;

    /// <summary>
    /// Casts a ray from a 2D screen pixel position to a point in world space.
    /// </summary>
    /// <param name="icp">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="cameraTransformMatrix">Transform matrix of the camera.</param>
    /// <param name="screenPoint">2D screen point to be cast.</param>
    /// <returns>The world space position where the ray intersects with the WorldMesh.</returns>
    public static Vector3 CastRayFromScreenToWorldPoint(XRCameraIntrinsics intrinsics, double[] distortion, Matrix4x4 cameraTransformMatrix, Vector2 screenPoint)
    {
        var width = intrinsics.resolution.x;
        var height = intrinsics.resolution.y;

        // Convert pixel coordinates to normalized viewport coordinates.
        var viewportPoint = new Vector2(screenPoint.x / width, screenPoint.y / height);

        return CastRayFromViewPortToWorldPoint(intrinsics, distortion, cameraTransformMatrix, viewportPoint);
    }

    /// <summary>
    /// Casts a ray from a 2D viewport position to a point in world space.
    /// This method is used as Unity's Camera.ScreenToWorld functions are limited to Unity's virtual cameras,
    /// whereas this method provides a raycast from the actual physical RGB camera.
    /// </summary>
    /// <param name="icp">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="cameraTransformMatrix">Transform matrix of the camera.</param>
    /// <param name="viewportPoint">2D viewport point to be cast.</param>
    /// <returns>The world space position where the ray intersects with the WorldMesh.</returns>
    public static Vector3 CastRayFromViewPortToWorldPoint(XRCameraIntrinsics intrinsics, double[] distortion, Matrix4x4 cameraTransformMatrix, Vector2 viewportPoint)
    {
        // Undistort the viewport point to account for lens distortion.
        var undistortedViewportPoint = UndistortViewportPoint(intrinsics, distortion, viewportPoint);

        // Create a ray based on the undistorted viewport point that projects out of the RGB camera.
        Ray ray = RayFromViewportPoint(intrinsics, undistortedViewportPoint, cameraTransformMatrix.GetPosition(), cameraTransformMatrix.rotation);

        // By default, set the hit point at a fixed length away.
        Vector3 hitPoint = ray.GetPoint(s_rayLength);

        // Raycast against the WorldMesh to find where the ray intersects.
        // TODO: Add a layer mask filter to prevent unwanted obstructions.
        if (Physics.Raycast(ray, out RaycastHit hit, 100, layerMask: 9))
        {
            hitPoint = hit.point;
            s_rayLength = hit.distance;
        }

        return hitPoint;
    }

    /// <summary>
    /// Undistorts a viewport point to account for lens distortion.
    /// https://en.wikipedia.org/wiki/Distortion_(optics)
    /// </summary>
    /// <param name="intrinsics">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="distortion">Distortion coefficients of the camera.</param>
    /// <param name="distortedViewportPoint">The viewport point that may have distortion.</param>
    /// <returns>The corrected/undistorted viewport point.</returns>
    public static Vector2 UndistortViewportPoint(XRCameraIntrinsics intrinsics, double[] distortion, Vector2 distortedViewportPoint)
    {
        var normalizedToPixel = new Vector2(intrinsics.resolution.x / 2f, intrinsics.resolution.y / 2f).magnitude;
        var pixelToNormalized = Mathf.Approximately(normalizedToPixel, 0) ? float.MaxValue : 1 / normalizedToPixel;
        var viewportToNormalized = new Vector2(intrinsics.resolution.x * pixelToNormalized, intrinsics.resolution.y * pixelToNormalized);
        var normalizedPrincipalPoint = intrinsics.principalPoint * pixelToNormalized;
        var normalizedToViewport = new Vector2(1 / viewportToNormalized.x, 1 / viewportToNormalized.y);

        Vector2 d = Vector2.Scale(distortedViewportPoint, viewportToNormalized);
        Vector2 o = d - normalizedPrincipalPoint;

        // Distortion coefficients.
        float K1 = (float)distortion[0];
        float K2 = (float)distortion[1];
        float P1 = (float)distortion[2];
        float P2 = (float)distortion[3];
        float K3 = (float)distortion[4];

        float r2 = o.sqrMagnitude;
        float r4 = r2 * r2;
        float r6 = r2 * r4;

        float radial = K1 * r2 + K2 * r4 + K3 * r6;
        Vector3 u = d + o * radial;

        // Tangential distortion correction.
        if (!Mathf.Approximately(P1, 0) || !Mathf.Approximately(P2, 0))
        {
            u.x += P1 * (r2 + 2 * o.x * o.x) + 2 * P2 * o.x * o.y;
            u.y += P2 * (r2 + 2 * o.y * o.y) + 2 * P1 * o.x * o.y;
        }

        return Vector2.Scale(u, normalizedToViewport);
    }

    /// <summary>
    /// Creates a ray projecting out from the RGB camera based on a viewport point.
    /// </summary>
    /// <param name="intrinsics">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="viewportPoint">2D viewport point to create the ray from.</param>
    /// <param name="cameraPos">Position of the camera.</param>
    /// <param name="cameraRotation">Rotation of the camera.</param>
    /// <returns>The created ray based on the viewport point.</returns>
    public static Ray RayFromViewportPoint(XRCameraIntrinsics intrinsics, Vector2 viewportPoint, Vector3 cameraPos, Quaternion cameraRotation)
    {
        var width = intrinsics.resolution.x;
        var height = intrinsics.resolution.y;
        var principalPoint = intrinsics.principalPoint;
        var focalLength = intrinsics.focalLength;

        Vector2 pixelPoint = new Vector2(viewportPoint.x * width, viewportPoint.y * height);
        Vector2 offsetPoint = new Vector2(pixelPoint.x - principalPoint.x, pixelPoint.y - (height - principalPoint.y));
        Vector2 unitFocalLength = new Vector2(offsetPoint.x / focalLength.x, offsetPoint.y / focalLength.y);

        Vector3 rayDirection = cameraRotation * new Vector3(unitFocalLength.x, unitFocalLength.y, 1).normalized;

        return new Ray(cameraPos, rayDirection);
    }

    /// <summary>
    /// Converts a 3D world position into a 2D screen pixel coordinate.
    /// </summary>
    /// <param name="intrinsics">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="cameraTransformMatrix">Transform matrix of the camera.</param>
    /// <param name="worldPoint">3D world point to be converted.</param>
    /// <returns>The screen pixel coordinates corresponding to the given world space position.</returns>
    public static Vector2 ConvertWorldPointToScreen(XRCameraIntrinsics intrinsics, Matrix4x4 cameraTransformMatrix, Vector3 worldPoint)
    {
        // Inverse the camera transformation to bring the world point into the camera's local space
        Vector3 localPoint = cameraTransformMatrix.inverse.MultiplyPoint3x4(worldPoint);

        // Project the local 3D point to the camera's 2D plane
        Vector2 cameraPlanePoint = ProjectPointToCameraPlane(intrinsics, localPoint);

        // Convert camera plane coordinates to pixel coordinates
        Vector2 pixelCoordinates = ConvertCameraPlanePointToPixel(intrinsics, cameraPlanePoint);
        return pixelCoordinates;
    }

    /// <summary>
    /// Projects a point from 3D space onto the camera's 2D plane using the camera's intrinsic parameters.
    /// </summary>
    /// <param name="intrinsics">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="point">The point in the camera's local space.</param>
    /// <returns>The point on the camera plane.</returns>
    private static Vector2 ProjectPointToCameraPlane(XRCameraIntrinsics intrinsics, Vector3 point)
    {
        // Normalize the point by the depth to project it onto the camera plane
        Vector2 normalizedPoint = new Vector2(point.x / point.z, point.y / point.z);

        // Apply the camera's intrinsic parameters to map the normalized point to the camera plane
        Vector2 cameraPlanePoint = new Vector2(
            normalizedPoint.x * intrinsics.focalLength.x + intrinsics.principalPoint.x,
            normalizedPoint.y * intrinsics.focalLength.y + intrinsics.principalPoint.y
        );

        return cameraPlanePoint;
    }

    /// <summary>
    /// Converts a point from the camera plane to pixel coordinates.
    /// </summary>
    /// <param name="intrinsics">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="point">The point on the camera plane.</param>
    /// <returns>The corresponding pixel coordinates.</returns>
    private static Vector2 ConvertCameraPlanePointToPixel(XRCameraIntrinsics intrinsics, Vector2 point)
    {
        // Convert the camera plane point to pixel coordinates by accounting for the image dimensions
        Vector2 pixelCoordinates = new Vector2(
            (point.x - intrinsics.principalPoint.x + intrinsics.resolution.x / 2f),
            (intrinsics.resolution.y / 2f - (point.y - intrinsics.principalPoint.y))
        );

        return pixelCoordinates;
    }
}
