// Copyright 2022-2025 Niantic.

#if NIANTIC_LIGHTSHIP_SHAREDAR_ENABLED

using System.IO;
using Niantic.Lightship.AR.NavigationMesh;
using Niantic.Lightship.SharedAR.Colocalization;
using Niantic.Lightship.SharedAR.Networking;
using Niantic.Lightship.SharedAR.Rooms;
using UnityEngine;

using ItemType = Niantic.Lightship.MagicLeap.InternalSamples.NetworkItem.NetworkItemType;

namespace Niantic.Lightship.MagicLeap.InternalSamples
{
    public class SimpleNetworking : MonoBehaviour
    {
        private IRoom _room;
        private LightshipNetworking _networking;

        private bool _gameStarted = false;

        public bool GameStarted => _gameStarted;

        private GameObject _sharedSpaceOrigin;

        public GameObject SharedSpaceOrigin
        {
            get { return _sharedSpaceOrigin; }
        }

        private SharedSpaceManager _sharedSpaceManager;
        private LightshipNavMeshManager _navMeshManager;

        public enum MessageType : uint
        {
            NetworkStarted = 0,
            SpawnWorld,
            SpawnItem,
            DestoryItem,
            UpdateTransform,
            Max
        };

        public GameObject[] _prefabs;
        public NetworkItem[] _networkPrefabs;
        private uint[] _itemCtr = new uint[(uint)ItemType.Max];

        public PeerID SelfPeerId()
        {
            return _networking.SelfPeerID;
        }

        public struct NetworkMessage
        {
            public uint Type;
            public uint SubType;
            public Vector3 Position;
            public Quaternion Rotation;
            public string Name;
            public byte RandomFunByte;

            public NetworkMessage(MessageType a_type, string a_name, ItemType a_subType = ItemType.Sphere, Vector3 a_pos = new Vector3(), Quaternion a_rot = new Quaternion(), byte fun = 0)
            {
                Type = (uint)a_type;
                SubType = (uint)a_subType;
                Position = a_pos;
                Rotation = a_rot;
                Name = a_name;
                RandomFunByte = fun;
            }
        };

        byte[] ToBytes(NetworkMessage str)
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);

            writer.Write(str.Type);
            writer.Write(str.SubType);
            writer.Write(str.Position.x);
            writer.Write(str.Position.y);
            writer.Write(str.Position.z);
            writer.Write(str.Rotation.x);
            writer.Write(str.Rotation.y);
            writer.Write(str.Rotation.z);
            writer.Write(str.Rotation.w);
            writer.Write(str.Name);
            writer.Write(str.RandomFunByte);

            var bytes = stream.ToArray();

            writer.Close();
            stream.Close();

            return bytes;
        }

        NetworkMessage FromBytes(byte[] arr)
        {
            NetworkMessage msg = new NetworkMessage();

            var stream = new MemoryStream(arr);
            var reader = new BinaryReader(stream);

            msg.Type = reader.ReadUInt32();
            msg.SubType = reader.ReadUInt32();

            float px = reader.ReadSingle();
            float py = reader.ReadSingle();
            float pz = reader.ReadSingle();
            msg.Position = new Vector3(px, py, pz);

            float rx = reader.ReadSingle();
            float ry = reader.ReadSingle();
            float rz = reader.ReadSingle();
            float rw = reader.ReadSingle();
            msg.Rotation = new Quaternion(rx, ry, rz, rw);
            msg.Name = reader.ReadString();
            msg.RandomFunByte = reader.ReadByte();

            reader.Close();
            stream.Close();

            return msg;
        }

        public void OnGameStart(GameObject sharedSpaceOrigin, LightshipNetworking networking)
        {
            _sharedSpaceOrigin = sharedSpaceOrigin;
            _networking = networking;
            _networking.DataReceived += Received;
            _gameStarted = true;
        }

        private void OnDestroy()
        {
            if (_networking != null)
            {
                _networking.DataReceived -= Received;
            }
        }

        private void SendMessage(NetworkMessage message)
        {
            _networking.SendData(_networking.PeerIDs, message.Type, ToBytes(message));
        }

        void SpawnNetworkItem(NetworkMessage message)
        {
            var obj = Instantiate
                (_networkPrefabs[message.SubType], _sharedSpaceOrigin.transform, false);

            obj.transform.SetLocalPositionAndRotation(message.Position, message.Rotation);
            obj.name = message.Name;
        }

        public void SpawnItem(ItemType item, Vector3 localPosition, Quaternion localRotation)
        {
            var obj = Instantiate(_prefabs[(int)item], _sharedSpaceOrigin.transform, false);
            obj.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            obj.name = item.ToString() + _itemCtr[(int)item] + "_" + _networking.SelfPeerID.GetHashCode();
            _itemCtr[(int)item]++;

            //tell other clients to spawn it.
            SendMessage(new NetworkMessage(MessageType.SpawnItem, obj.name, item, localPosition, localRotation));
        }

        public void DestroyItem(GameObject obj)
        {
            Destroy(obj);
            SendMessage(new NetworkMessage(MessageType.DestoryItem, obj.name));
        }

        // Global Search TODO: Use Object Pool with ID
        public void DestroyNetworkItem(NetworkMessage message)
        {
            var items = FindObjectsOfType<NetworkItem>();

            foreach (var item in items)
            {
                if (item.name == message.Name)
                {
                    Destroy(item.gameObject);
                    return;
                }
            }
        }

        private void Received(DataReceivedArgs obj)
        {
            //just in case.
            if (_networking.SelfPeerID == obj.PeerID)
                return;

            if (obj.Tag < (uint)MessageType.NetworkStarted || obj.Tag > (uint)MessageType.Max)
            {
                return;
            }

            var data = obj.CopyData();
            NetworkMessage message = FromBytes(data);
            MessageType type = (MessageType)obj.Tag;

            switch (type)
            {
                case MessageType.SpawnItem:
                    SpawnNetworkItem(message);
                    break;
                case MessageType.DestoryItem:
                    DestroyNetworkItem(message);
                    break;
                case MessageType.UpdateTransform:
                    break;
            }
        }
    }
}

#endif
