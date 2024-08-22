using Boo.Lang.Runtime.DynamicDispatching;
using NetMQ;
using System;
using UnityEngine;
using UnityMainThreadDispatcher;

namespace PubSub
{
    public class KinectSubscriber : MonoBehaviour
    {
        private Subscriber subscriber;

        [SerializeField] private string host = "10.190.195.213";
        [SerializeField] private string port = "12345";

        public GameObject kinectCube;
        private Renderer cubeRenderer;

        void Start()
        {
            Debug.Log("Starting subscriber...");
            subscriber = new Subscriber(host, port);
            subscriber.AddTopicCallback("ColorFrame", data => OnColorFrameReceived(data));
            subscriber.AddTopicCallback("DepthFrame", data => OnDepthFrameReceived(data));
            subscriber.AddTopicCallback("ColorInDepthFrame", data => OnColorInDepthFrameReceived(data));
            Debug.Log("Subscriber setup complete.");

            // Initialize the Cube object and its Renderer
            if (kinectCube != null)
            {
                cubeRenderer = kinectCube.GetComponent<Renderer>();
            }
            else
            {
                Debug.LogError("KinectCube is not assigned in the inspector.");
            }
        }

        private void OnDestroy()
        {
            Debug.Log("Destroying subscriber...");
            subscriber.Dispose();
        }

        // Methods to handle received data
        private void OnColorFrameReceived(byte[] data)
        {
            Debug.Log($"Received ColorFrame, data length: {data.Length}");
            UnityMainThreadDispatcher.Dispatcher.Enqueue(() => ApplyColorFrameToCube(data));
        }

        private void OnDepthFrameReceived(byte[] data)
        {
            Debug.Log($"Received DepthFrame, data length: {data.Length}");
        }

        private void OnColorInDepthFrameReceived(byte[] data)
        {
            Debug.Log($"Received ColorInDepthFrame, data length: {data.Length}");
        }

        private void ApplyColorFrameToCube(byte[] data)
        {
            if (cubeRenderer != null)
            {
                // Assume ColorFrame data is in BGRA32 format
                int width = 1920; // Adjust based on Kinect configuration
                int height = 1080; // Adjust based on Kinect configuration

                Texture2D texture = new Texture2D(width, height, TextureFormat.BGRA32, false);
                texture.LoadRawTextureData(data);
                texture.Apply();

                cubeRenderer.material.mainTexture = texture;
            }
            else
            {
                Debug.LogWarning("Cube renderer is not initialized.");
            }
        }
    }
}
