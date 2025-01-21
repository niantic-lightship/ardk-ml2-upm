// Copyright 2022-2024 Niantic.

#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using UnityEngine.Android;

namespace Niantic.Lightship.MagicLeap
{
    public static class PermissionRequester
    {
        public static void RequestPermission
        (
            string permission,
            Action<string> onPermissionGranted,
            Action<string> onPermissionDenied,
            Action<string> onPermissionDeniedAndDontAskAgain = null
        )
        {
            if (!Permission.HasUserAuthorizedPermission(permission))
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += onPermissionGranted;
                callbacks.PermissionDenied += onPermissionDenied;
                callbacks.PermissionDeniedAndDontAskAgain += onPermissionDeniedAndDontAskAgain ?? onPermissionDenied;

                Permission.RequestUserPermission(permission, callbacks);
            }
            else
            {
                onPermissionGranted(permission);
            }
        }

        public static void RequestPermissions
        (
            string[] permissions,
            Action<string> onPermissionGranted,
            Action<string> onPermissionDenied,
            Action<string> onPermissionDeniedAndDontAskAgain = null
        )
        {
            if (permissions == null || permissions.Length == 0)
            {
                throw new ArgumentException(nameof(permissions));
            }

            var neededPermissions = new List<string>();
            foreach (var p in permissions)
            {
                if (!Permission.HasUserAuthorizedPermission(p))
                {
                    neededPermissions.Add(p);
                }
                else
                {
                    onPermissionGranted(p);
                }
            }

            if (neededPermissions.Count == 0)
            {
                return;
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += onPermissionGranted;
            callbacks.PermissionDenied += onPermissionDenied;
            callbacks.PermissionDeniedAndDontAskAgain += onPermissionDeniedAndDontAskAgain ?? onPermissionDenied;

            Permission.RequestUserPermissions(permissions);
        }
    }
}
#endif
