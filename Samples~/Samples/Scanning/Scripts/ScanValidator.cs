// Copyright 2022-2025 Niantic.
using System.IO;
using System.Text;
using Niantic.ARDK.AR.Scanning;
using UnityEngine;

namespace Niantic.Lightship.AR.Samples
{
    public class ScanValidator : MonoBehaviour
    {
        private int frameCount;
        private float startTime;
        private float stopTime;
        private bool isScanning;

        // Start is called before the first frame update
        private void Start()
        {

        }

        public void ScanStarted()
        {
            isScanning = true;
            startTime = Time.time;
            frameCount = 0;
        }

        public void ScanStopped()
        {
            isScanning = false;
            stopTime = Time.time;
        }

        public string ValidatedScan(ScanStore.SavedScan savedScan, string archivedPath)
        {
            var sizeBytes = new FileInfo(archivedPath).Length;
            float sizeInMb = sizeBytes / 1024.0f / 1024.0f;
            var frames = savedScan.GetScanFrames();
            int numberOfFrames = frames.Frames.Count;
            double durationOfScan = frames.Frames[numberOfFrames - 1].Timestamp - frames.Frames[0].Timestamp;
            float scanFps = numberOfFrames / (float) durationOfScan;
            float appFps = frameCount / (stopTime - startTime);
            return $"Size: {sizeInMb}MB; Scan Fps: {scanFps}; App Fps: {appFps}";
        }

        // Update is called once per frame
        private void Update()
        {
            if (isScanning)
            {
                frameCount++;
            }
        }
    }
}
