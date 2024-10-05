using UnityEngine;
using System;
using System.Collections.Generic;
using NetMQ;
using TMPro; // For UI Display
using System.Linq;

namespace PubSub
{
    public class KinectSubscriber : MonoBehaviour
    {
        public TextMeshProUGUI ByteLengthTMP;
        public Shader PointCloudShader;
        public Material PointcloudMat;

        private Subscriber subscriber;
        [SerializeField] private string host;
        [SerializeField] private string port = "55555";

        private List<Vector3> pointCloud;
        private bool hasReceivedFirstFrame = false;

        void Start()
        {
            try
            {
                subscriber = new Subscriber(host, port);
                subscriber.AddTopicCallback("PointCloud", data => OnPointCloudReceived(data));
                Debug.Log("Subscriber setup complete with host: " + host + " and port: " + port);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to start subscriber: " + e.Message);
            }
        }

        private void OnPointCloudReceived(byte[] data)
        {
            pointCloud = DeserializePointCloud(data);
            hasReceivedFirstFrame = true;
            Debug.Log($"Received point cloud data with {pointCloud.Count} points.");
        }

        private List<Vector3> DeserializePointCloud(byte[] data)
        {
            int pointCount = data.Length / (3 * sizeof(float));
            List<Vector3> pointCloud = new List<Vector3>(pointCount);

            for (int i = 0; i < pointCount; i++)
            {
                float x = BitConverter.ToSingle(data, i * 3 * sizeof(float));
                float y = BitConverter.ToSingle(data, i * 3 * sizeof(float) + sizeof(float));
                float z = BitConverter.ToSingle(data, i * 3 * sizeof(float) + 2 * sizeof(float));
                pointCloud.Add(new Vector3(x, y, z));
            }

            return pointCloud;
        }

        private void Update()
        {
            if (hasReceivedFirstFrame && pointCloud != null)
            {
                RenderPointCloud(pointCloud, PointcloudMat);
            }
        }

        private void RenderPointCloud(List<Vector3> pointCloud, Material pointCloudMat)
        {
            Mesh pointMesh = new Mesh();
            pointMesh.SetVertices(pointCloud);
            pointMesh.SetIndices(Enumerable.Range(0, pointCloud.Count).ToArray(), MeshTopology.Points, 0);

            Graphics.DrawMesh(pointMesh, Matrix4x4.identity, pointCloudMat, 0);
        }

        private void OnDestroy()
        {
            if (subscriber != null)
            {
                subscriber.Dispose();
                subscriber = null;
            }
        }
    }
}
