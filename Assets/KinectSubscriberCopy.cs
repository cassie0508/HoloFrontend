using NetMQ;
using System;
using System.Net.Sockets;
using System.Net;
using UnityEngine;
using UnityMainThreadDispatcher;

namespace PubSub
{
    public class KinectSubscriberCopy : MonoBehaviour
    {
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

        [Header("ReadOnly and exposed for Debugging: Update for every Frame")]
        [SerializeField] private Texture2D DepthImage;
        [SerializeField] private Texture2D ColorImage;
        [SerializeField] private Texture2D ColorInDepthImage;
        [SerializeField] private Material PointcloudMat;
        [SerializeField] private Material OcclusionMat;

        private Subscriber subscriber;

        [SerializeField] private string host;
        [SerializeField] private string port = "12345";

        public virtual void OnSetPointcloudProperties(Material pointcloudMat) { }

        void Start()
        {
            Debug.Log("Starting subscriber...");
            host = GetLocalIPAddress();
            subscriber = new Subscriber(host, port);
            subscriber.AddTopicCallback("Camera", data => OnCameraReceived(data));
            subscriber.AddTopicCallback("Frame", data => OnFrameReceived(data));
            Debug.Log("Subscriber setup complete.");
        }

        public string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        private void OnCameraReceived(byte[] data)
        {
            UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
            {
                // Parse camera data
                int xyLookupDataLength = BitConverter.ToInt32(data, 0);
                int calibrationDataLength = BitConverter.ToInt32(data, sizeof(int) * 1);
                int cameraSizeDataLength = BitConverter.ToInt32(data, sizeof(int) * 2);

                byte[] xyLookupData = new byte[xyLookupDataLength];
                Buffer.BlockCopy(data, sizeof(int) * 3, xyLookupData, 0, xyLookupDataLength);
                byte[] calibrationData = new byte[calibrationDataLength];
                Buffer.BlockCopy(data, sizeof(int) * 3 + xyLookupDataLength, calibrationData, 0, calibrationDataLength);
                byte[] cameraSizeData = new byte[cameraSizeDataLength];
                Buffer.BlockCopy(data, sizeof(int) * 3 + xyLookupDataLength + calibrationDataLength, cameraSizeData, 0, cameraSizeDataLength);

                // Setup texture
                int[] captureArray = new int[6];
                Buffer.BlockCopy(cameraSizeData, 0, captureArray, 0, cameraSizeData.Length);
                ColorWidth = captureArray[0];
                ColorHeight = captureArray[1];
                DepthWidth = captureArray[2];
                DepthHeight = captureArray[3];
                IRWidth = captureArray[4];
                IRHeight = captureArray[5];

                SetupTextures(ref ColorImage, ref DepthImage, ref ColorInDepthImage);   // TODO: ref or not?

                // Setup point cloud shader
                if(XYLookup == null)
                {
                    XYLookup = new Texture2D(DepthImage.width, DepthImage.height, TextureFormat.RGBAFloat, false);
                    XYLookup.LoadRawTextureData(xyLookupData);
                    XYLookup.Apply();
                }

                Color2DepthCalibration = ByteArrayToMatrix4x4(calibrationData);

                PointcloudMat = SetupPointcloudShader(PointCloudShader, ColorInDepthImage, DepthImage);
                OcclusionMat = SetupPointcloudShader(OcclusionShader, ColorInDepthImage, DepthImage);
            });
        }

        private void OnFrameReceived(byte[] data)
        {
            UnityMainThreadDispatcher.Dispatcher.Enqueue(() => {
                // Parse frame data
                int colorDataLength = BitConverter.ToInt32(data, 0);
                int depthDataLength = BitConverter.ToInt32(data, sizeof(int));
                int colorInDepthDataLength = BitConverter.ToInt32(data, sizeof(int) * 2);

                byte[] colorData = new byte[colorDataLength];
                Buffer.BlockCopy(data, sizeof(int) * 3, colorData, 0, colorDataLength);
                byte[] depthData = new byte[depthDataLength];
                Buffer.BlockCopy(data, sizeof(int) * 3 + colorDataLength, depthData, 0, depthDataLength);
                byte[] colorInDepthData = new byte[colorInDepthDataLength];
                Buffer.BlockCopy(data, sizeof(int) * 3 + colorDataLength + depthDataLength, colorInDepthData, 0, colorInDepthDataLength);

                // Apply data to textures
                ColorImage.LoadRawTextureData(colorData);
                ColorImage.Apply();
                DepthImage.LoadRawTextureData(depthData);
                DepthImage.Apply();
                ColorInDepthImage.LoadRawTextureData(colorInDepthData);
                ColorInDepthImage.Apply();

                // Set point cloud shader
                int pixel_count = DepthImage.width * DepthImage.height;
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
            });
        }

        private void SetupTextures(ref Texture2D Color, ref Texture2D Depth, ref Texture2D ColorInDepth)
        {
            if (Color == null)
                Color = new Texture2D(ColorWidth, ColorHeight, TextureFormat.BGRA32, false);
            if (Depth == null)
                Depth = new Texture2D(DepthWidth, DepthHeight, TextureFormat.R16, false);
            if (ColorInDepth == null)
                ColorInDepth = new Texture2D(IRWidth, IRHeight, TextureFormat.BGRA32, false);
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

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {

            if (EnableARBackground && ARBackgroundMaterial)
            {
                Graphics.Blit(ColorImage, destination, new Vector2(1, -1), Vector2.zero);
                Graphics.Blit(source, destination, ARBackgroundMaterial);
            }
            else
                Graphics.Blit(source, destination);
        }

        private void OnDestroy()
        {
            Debug.Log("Destroying subscriber...");
            subscriber.Dispose();
        }
    }
}
