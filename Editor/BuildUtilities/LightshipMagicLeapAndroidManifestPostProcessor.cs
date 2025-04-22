// Copyright 2022-2025 Niantic.

using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Niantic.Lightship.MagicLeap.Editor
{

    // So we need to remove an extra launch activity from the AndroidManifest.xml file.
    // ML setup tool appears to do this in a SetupStep so a large callbackOrder isn't really necessary but just in case.
    internal class LightshipMagicLeapAndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 999;

        public const string PROJECT_MANIFEST_PATH = "Assets/Plugins/Android/AndroidManifest.xml";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
           if (!File.Exists(PROJECT_MANIFEST_PATH))
           {
               Debug.LogWarning("AndroidManifest.xml not found at path: " + PROJECT_MANIFEST_PATH);
               return;
           }

           var xmlDocument = new XmlDocument();
           xmlDocument.Load(PROJECT_MANIFEST_PATH);
           XmlNamespaceManager namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
           namespaceManager.AddNamespace("android", "http://schemas.android.com/apk/res/android");

           // MagicLeap Setup Tool copies over the example Android Manifest for the current Unity Editor.
           // In Unity 6, that example includes *two* launcher Activities - one for UnityPlayerActivity and one for
           // UnityPlayerGameActivity, expecting the user to remove one or the other.
           // This causes a black screen for ML2 apps.
           // Here we remove all but the first launcher activity.
           XmlNodeList launcherActivities = xmlDocument.SelectNodes(
               "/manifest/application/activity[intent-filter/category[@android:name='android.intent.category.LAUNCHER']]",
               namespaceManager
           );
           if (launcherActivities == null)
           {
               Debug.LogWarning("Could not find any Android Launcher Activities in manifest at: " + PROJECT_MANIFEST_PATH);
           }

           for (int i = 1; i < launcherActivities.Count; i++)
           {
               // if we have more than 1 launcher activity, delete the others.
               launcherActivities[i].ParentNode.RemoveChild(launcherActivities[i]);
           }

           xmlDocument.Save(PROJECT_MANIFEST_PATH);
        }
    }
}
