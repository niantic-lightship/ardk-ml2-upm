// Copyright 2022-2025 Niantic.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.Lightship.MagicLeap.InternalSamples
{
    public class NetworkItem : MonoBehaviour
    {
        public enum NetworkItemType
        {
            Cube,
            Sphere,
            Max
        }

        private NetworkItemType _type;

        public NetworkItemType Type
        {
            get { return _type; }
            set { _type = value; }
        }

        private string _id;

        public string Id
        {
            get { return _id; }
            set { _id = value; }
        }
    }
}
