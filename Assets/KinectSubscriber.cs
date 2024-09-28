using NetMQ;
using System;
using System.Net.Sockets;
using System.Net;
using UnityEngine;
using UnityMainThreadDispatcher;
using UnityEngine.UI;

namespace PubSub
{
    public class KinectSubscriber : MonoBehaviour
    {
        public RawImage ConnectionIndicator;

        [Header("Pointcloud Configs")]
        public bool UseOcclusionShader = true;
        public Shader PointCloudShader;
        public Shader OcclusionShader;
        [Range(0.01f, 0.1f)]
        public float MaxPointDistance = 0.02f;

        [Header("Background Configs\n(Only works if this script is attached onto the camera)")]
        public bool EnableARBackground = true;
        [Tooltip("Only needs to be set when BlitToCamera is checked")]
        public Material ARBackgroundMaterial;

        [Header("ReadOnly and exposed for Debugging: Initial Message")]
        [SerializeField] private int ColorWidth;
        [SerializeField] private int ColorHeight;
        [SerializeField] private int DepthWidth;
        [SerializeField] private int DepthHeight;
        [SerializeField] private int IRWidth;
        [SerializeField] private int IRHeight;
        [SerializeField] private Texture2D XYLookup;
        [SerializeField] private Matrix4x4 Color2DepthCalibration;

        private byte[] xyLookupDataPart1;
        private byte[] xyLookupDataPart2;
        private byte[] xyLookupDataPart3;

        [Header("ReadOnly and exposed for Debugging: Update for every Frame")]
        [SerializeField] private Texture2D DepthImage;
        [SerializeField] private Texture2D ColorInDepthImage;
        [SerializeField] private Material PointcloudMat;
        [SerializeField] private Material OcclusionMat;

        private Subscriber subscriber;

        [SerializeField] private string host;
        [SerializeField] private string port = "55555";

        public virtual void OnSetPointcloudProperties(Material pointcloudMat) { }

        void Start()
        {
            try
            {
                subscriber = new Subscriber(host, port);

                subscriber.AddTopicCallback("Camera", data => OnCameraReceived(data));
                subscriber.AddTopicCallback("Lookup1", data => OnLookupsReceived(data, 1));
                subscriber.AddTopicCallback("Lookup2", data => OnLookupsReceived(data, 2));
                subscriber.AddTopicCallback("Lookup3", data => OnLookupsReceived(data, 3));
                subscriber.AddTopicCallback("Frame", data => OnFrameReceived(data));
                Debug.Log("Subscriber setup complete with host: " + host + " and port: " + port);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to start subscriber: " + e.Message);
            }
        }

        private void OnCameraReceived(byte[] data)
        {
            Debug.Log("On Camera Received: Data length : " + data.Length);

            UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
            {
                if (ConnectionIndicator)
                    ConnectionIndicator.color = Color.green;

                try
                {
                    // Parse camera data
                    int calibrationDataLength = BitConverter.ToInt32(data, 0);
                    int cameraSizeDataLength = BitConverter.ToInt32(data, sizeof(int) * 1);

                    byte[] calibrationData = new byte[calibrationDataLength];
                    Buffer.BlockCopy(data, sizeof(int) * 2, calibrationData, 0, calibrationDataLength);
                    byte[] cameraSizeData = new byte[cameraSizeDataLength];
                    Buffer.BlockCopy(data, sizeof(int) * 2 + calibrationDataLength, cameraSizeData, 0, cameraSizeDataLength);

                    // Setup texture
                    int[] captureArray = new int[6];
                    Buffer.BlockCopy(cameraSizeData, 0, captureArray, 0, cameraSizeData.Length);
                    ColorWidth = captureArray[0];
                    ColorHeight = captureArray[1];
                    DepthWidth = captureArray[2];
                    DepthHeight = captureArray[3];
                    IRWidth = captureArray[4];
                    IRHeight = captureArray[5];

                    SetupTextures(ref DepthImage, ref ColorInDepthImage);

                    Color2DepthCalibration = ByteArrayToMatrix4x4(calibrationData);
                }
                catch (Exception e)
                {
                    Debug.LogError("Error in OnCameraReceived: " + e.Message);
                }
            });
        }

        private void OnLookupsReceived(byte[] data, int part)
        {
            Debug.Log("On Lookup Received part: " + part + " with length " + data.Length);

            UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
            {
                if (part == 1) xyLookupDataPart1 = data;
                if (part == 2) xyLookupDataPart2 = data;
                if (part == 3)
                {
                    xyLookupDataPart3 = data;

                    byte[] xyLookupData = new byte[xyLookupDataPart1.Length + xyLookupDataPart2.Length + xyLookupDataPart3.Length];
                    System.Buffer.BlockCopy(xyLookupDataPart1, 0, xyLookupData, 0, xyLookupDataPart1.Length);
                    System.Buffer.BlockCopy(xyLookupDataPart2, 0, xyLookupData, xyLookupDataPart1.Length, xyLookupDataPart2.Length);
                    System.Buffer.BlockCopy(xyLookupDataPart3, 0, xyLookupData, xyLookupDataPart1.Length + xyLookupDataPart2.Length, xyLookupDataPart3.Length);

                    if (XYLookup == null)
                    {
                        XYLookup = new Texture2D(DepthImage.width, DepthImage.height, TextureFormat.RGBAFloat, false);
                        XYLookup.LoadRawTextureData(xyLookupData);
                        XYLookup.Apply();

                        Debug.Log("XYLookup data length " + xyLookupData.Length);

                        PointcloudMat = SetupPointcloudShader(PointCloudShader, ColorInDepthImage, DepthImage);
                        OcclusionMat = SetupPointcloudShader(OcclusionShader, ColorInDepthImage, DepthImage);
                        OcclusionMat.renderQueue = 3000;    // Set renderQueue to avoid rendering artifact
                    }
                }
            });
        }

        private void OnFrameReceived(byte[] data)
        {
            Debug.Log("On Frame Received: Data length : " + data.Length);

            UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
            {
                try
                {
                    // Parse frame data
                    int depthDataLength = BitConverter.ToInt32(data, 0);
                    int colorInDepthDataLength = BitConverter.ToInt32(data, sizeof(int));
                    Debug.Log("depthData length: " + depthDataLength + "   colorInDepthData length: " + colorInDepthDataLength);

                    byte[] depthData = new byte[depthDataLength];
                    Buffer.BlockCopy(data, sizeof(int) * 2, depthData, 0, depthDataLength);
                    byte[] colorInDepthData = new byte[colorInDepthDataLength];
                    Buffer.BlockCopy(data, sizeof(int) * 2 + depthDataLength, colorInDepthData, 0, colorInDepthDataLength);

                    // Apply data to textures
                    DepthImage.LoadRawTextureData(depthData);
                    DepthImage.Apply();
                    ColorInDepthImage.LoadImage(colorInDepthData);
                    ColorInDepthImage.Apply();
                }
                catch (Exception e)
                {
                    Debug.LogError("Error in OnFrameReceived: " + e.Message);
                }
            });
        }

        protected virtual void Update()
        {
            if (DepthImage != null && PointcloudMat != null)
            {
                int pixel_count = DepthImage.width * DepthImage.height;

                try
                {
                    // Set point cloud shader properties and render point cloud every frame
                    PointcloudMat.SetMatrix("_PointcloudOrigin", transform.localToWorldMatrix);
                    PointcloudMat.SetFloat("_MaxPointDistance", MaxPointDistance);

                    OnSetPointcloudProperties(PointcloudMat);

                    if (!UseOcclusionShader)
                    {
                        PointcloudMat.EnableKeyword("_ORIGINALPC_ON");
                    }
                    else
                    {
                        PointcloudMat.DisableKeyword("_ORIGINALPC_ON");

                        OcclusionMat.SetMatrix("_PointcloudOrigin", transform.localToWorldMatrix);
                        OcclusionMat.SetFloat("_MaxPointDistance", MaxPointDistance);
                        Graphics.DrawProcedural(OcclusionMat, new Bounds(transform.position, Vector3.one * 10), MeshTopology.Points, pixel_count);
                    }

                    Graphics.DrawProcedural(PointcloudMat, new Bounds(transform.position, Vector3.one * 10), MeshTopology.Points, pixel_count);            
                }
                catch (Exception e)
                {
                    Debug.LogError("Error in point cloud rendering: " + e.Message);
                }
            }
        }

        private void SetupTextures(ref Texture2D Depth, ref Texture2D ColorInDepth)
        {
            Debug.Log("Setting up textures: DepthWidth=" + DepthWidth + " DepthHeight=" + DepthHeight);

            if (Depth == null)
            {
                Depth = new Texture2D(DepthWidth, DepthHeight, TextureFormat.R8, false);
            }

            if (ColorInDepth == null)
            {
                ColorInDepth = new Texture2D(IRWidth, IRHeight, TextureFormat.BGRA32, false);
            }
        }


        private Matrix4x4 ByteArrayToMatrix4x4(byte[] byteArray)
        {
            float[] matrixFloats = new float[16];
            Buffer.BlockCopy(byteArray, 0, matrixFloats, 0, byteArray.Length);

            Matrix4x4 matrix = new Matrix4x4();
            for (int i = 0; i < 16; i++)
            {
                matrix[i] = matrixFloats[i];
            }

            return matrix;
        }

        private Material SetupPointcloudShader(Shader shader, Texture2D ColorInDepth, Texture2D Depth)
        {
            var PointCloudMat = new Material(shader);

            PointCloudMat.SetPass(0);

            PointCloudMat.SetTexture("_ColorTex", ColorInDepth);
            PointCloudMat.SetInt("_ColorWidth", ColorInDepth.width);
            PointCloudMat.SetInt("_ColorHeight", ColorInDepth.height);

            PointCloudMat.SetTexture("_DepthTex", Depth);
            PointCloudMat.SetInt("_DepthWidth", Depth.width);
            PointCloudMat.SetInt("_DepthHeight", Depth.height);

            PointCloudMat.SetTexture("_XYLookup", XYLookup);
            PointCloudMat.SetMatrix("_Col2DepCalibration", Color2DepthCalibration);

            return PointCloudMat;
        }

        private void OnDestroy()
        {
            Debug.Log("Destroying subscriber...");
            if (subscriber != null)
            {
                subscriber.Dispose();
                subscriber = null;
            }
        }
    }
}
