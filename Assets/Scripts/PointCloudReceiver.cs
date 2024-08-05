using System.Collections;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;

public class PointCloudReceiver : MonoBehaviour
{
    public Transform Origin;
    public Transform Duplica;

    private SubscriberSocket subscriberSocket;
    private Texture2D receivedColorTexture;
    private Texture2D receivedDepthTexture;

    private void Start()
    {
        subscriberSocket = new SubscriberSocket();
        subscriberSocket.Connect("tcp://192.168.1.102:12345");
        subscriberSocket.Subscribe("");

        receivedColorTexture = new Texture2D(1920, 1080, TextureFormat.BGRA32, false);
        receivedDepthTexture = new Texture2D(512, 512, TextureFormat.R16, false);

        StartCoroutine(ReceiveData());
    }

    private IEnumerator ReceiveData()
    {
        while (true)
        {
            byte[] colorData = subscriberSocket.ReceiveFrameBytes();
            byte[] depthData = subscriberSocket.ReceiveFrameBytes();

            receivedColorTexture.LoadRawTextureData(colorData);
            receivedColorTexture.Apply();

            receivedDepthTexture.LoadRawTextureData(depthData);
            receivedDepthTexture.Apply();

            UpdatePointCloud(Origin, receivedColorTexture, receivedDepthTexture);
            UpdatePointCloud(Duplica, receivedColorTexture, receivedDepthTexture);

            yield return null;
        }
    }

    private void UpdatePointCloud(Transform pointCloudTransform, Texture2D colorTexture, Texture2D depthTexture)
    {
        Material pointCloudMaterial = pointCloudTransform.GetComponent<Renderer>().material;
        pointCloudMaterial.SetTexture("_ColorTex", colorTexture);
        pointCloudMaterial.SetTexture("_DepthTex", depthTexture);
        pointCloudMaterial.SetMatrix("_PointcloudOrigin", pointCloudTransform.localToWorldMatrix);
    }

    private void OnDestroy()
    {
        subscriberSocket.Close();
    }
}
