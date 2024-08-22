using NetMQ;
using System;
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

        [Header("Duplicated Reality")]
        public bool EnableDuplication = true;
        public Transform RegionOfInterest;
        public Transform DuplicatedReality;

        [Header("Background Configs\n(Only works if this script is attached onto the camera)")]
        public bool EnableARBackground = true;
        [Tooltip("Only needs to be set when BlitToCamera is checked")]
        public Material ARBackgroundMaterial;

        [Header("ReadOnly and exposed for Debugging")]
        [SerializeField] private Texture2D DepthImage;
        [SerializeField] private Texture2D ColorImage;
        [SerializeField] private Texture2D ColorInDepthImage;
        [SerializeField] private int ColorWidth;
        [SerializeField] private int ColorHeight;
        [SerializeField] private int DepthWidth;
        [SerializeField] private int DepthHeight;
        [SerializeField] private int IRWidth;
        [SerializeField] private int IRHeight;
        [SerializeField] private Texture2D xylookup;
        [SerializeField] private Matrix4x4 color2DepthCalibration;
        [SerializeField] private Material PointcloudMat;
        [SerializeField] private Material OcclusionMat;

        private Subscriber subscriber;

        [SerializeField] private string host = "10.32.211.162";
        [SerializeField] private string port = "12345";


        void Start()
        {
            Debug.Log("Starting subscriber...");
            subscriber = new Subscriber(host, port);
            subscriber.AddTopicCallback("XYTable", data => OnXYTableReceived(data));
            subscriber.AddTopicCallback("Color2DepthCalibration", data => OnColor2DepthCalibrationReceived(data));
            subscriber.AddTopicCallback("Capture", data => OnCaptureReceived(data));
            subscriber.AddTopicCallback("AllFrames", data => OnAllFramesReceived(data));
            Debug.Log("Subscriber setup complete.");
        }

        private void OnColor2DepthCalibrationReceived(byte[] data)
        {
            UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
            {
                color2DepthCalibration = ByteArrayToMatrix4x4(data);
                Debug.Log("Received and applied Color2DepthCalibration.");
            });
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

        private void OnXYTableReceived(byte[] data)
        {
            UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
            {
                if (xylookup == null || xylookup.width * xylookup.height != data.Length / sizeof(float) / 4)
                {
                    //xylookup = new Texture2D(DepthImage.width, DepthImage.height, TextureFormat.RGBAFloat, false);
                    xylookup = new Texture2D(640, 576, TextureFormat.RGBAFloat, false);
                }

                xylookup.LoadRawTextureData(data);
                xylookup.Apply();

                Debug.Log("Received and applied XYTable data.");
            });
        }


        private void OnAllFramesReceived(byte[] data)
        {
            UnityMainThreadDispatcher.Dispatcher.Enqueue(() => {
                int colorDataLength = BitConverter.ToInt32(data, 0);
                int depthDataLength = BitConverter.ToInt32(data, sizeof(int));
                int colorInDepthDataLength = BitConverter.ToInt32(data, sizeof(int) * 2);

                byte[] colorData = new byte[colorDataLength];
                byte[] depthData = new byte[depthDataLength];
                byte[] colorInDepthData = new byte[colorInDepthDataLength];

                Buffer.BlockCopy(data, sizeof(int) * 3, colorData, 0, colorDataLength);
                Buffer.BlockCopy(data, sizeof(int) * 3 + colorDataLength, depthData, 0, depthDataLength);
                Buffer.BlockCopy(data, sizeof(int) * 3 + colorDataLength + depthDataLength, colorInDepthData, 0, colorInDepthDataLength);

                ColorImage.LoadRawTextureData(colorData);
                ColorImage.Apply();

                DepthImage.LoadRawTextureData(depthData);
                DepthImage.Apply();

                ColorInDepthImage.LoadRawTextureData(colorInDepthData);
                ColorInDepthImage.Apply();

                int pixel_count = DepthImage.width * DepthImage.height;
                PointcloudMat.SetMatrix("_PointcloudOrigin", transform.localToWorldMatrix);
                PointcloudMat.SetFloat("_MaxPointDistance", MaxPointDistance);

                if (EnableDuplication) PointcloudMat.EnableKeyword("_DUPLICATE_ON");
                else PointcloudMat.DisableKeyword("_DUPLICATE_ON");

                PointcloudMat.SetMatrix("_Roi2Dupl", DuplicatedReality.localToWorldMatrix * RegionOfInterest.worldToLocalMatrix);
                PointcloudMat.SetMatrix("_ROI_Inversed", RegionOfInterest.worldToLocalMatrix);
                PointcloudMat.SetMatrix("_Dupl_Inversed", DuplicatedReality.worldToLocalMatrix);

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

        private void OnCaptureReceived(byte[] data)
        {
            UnityMainThreadDispatcher.Dispatcher.Enqueue(() => {
                int[] capture = new int[6];
                Buffer.BlockCopy(data, 0, capture, 0, data.Length);

                ColorWidth = capture[0];
                ColorHeight = capture[1];
                DepthWidth = capture[2];
                DepthHeight = capture[3];
                IRWidth = capture[4];
                IRHeight = capture[5];

                Debug.Log(capture);

                Debug.Log($"Received Capture Dimensions: ColorWidth={ColorWidth}, ColorHeight={ColorHeight}, DepthWidth={DepthWidth}, DepthHeight={DepthHeight}, IRWidth={IRWidth}, IRHeight={IRHeight}");

                SetupTextures(ref ColorImage, ref DepthImage, ref ColorInDepthImage);

                PointcloudMat = SetupPointcloudShader(PointCloudShader, ColorInDepthImage, ref DepthImage);
                OcclusionMat = SetupPointcloudShader(OcclusionShader, ColorInDepthImage, ref DepthImage);
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

        private Material SetupPointcloudShader(Shader shader, Texture2D ColorInDepth, ref Texture2D Depth)
        {
            var PointCloudMat = new Material(shader);

            PointCloudMat.SetPass(0);

            PointCloudMat.SetTexture("_ColorTex", ColorInDepth);
            PointCloudMat.SetInt("_ColorWidth", ColorInDepth.width);
            PointCloudMat.SetInt("_ColorHeight", ColorInDepth.height);

            PointCloudMat.SetTexture("_DepthTex", Depth);
            PointCloudMat.SetInt("_DepthWidth", Depth.width);
            PointCloudMat.SetInt("_DepthHeight", Depth.height);

            PointCloudMat.SetTexture("_XYLookup", xylookup);

            
            PointCloudMat.SetMatrix("_Col2DepCalibration", color2DepthCalibration);

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

        private void OnTestMessage(string topic, byte[] data)
        {
            Debug.Log($"Received data on topic: {topic}, data length: {data.Length}");
        }
    }
}
