﻿using PubSub;
using System;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PBM_CaptureCamera : MonoBehaviour
{
    [SerializeField] private string host;
    [SerializeField] private string port = "55555";
    private Subscriber subscriber;

    [Header("Feed the camera texture into ColorImage. \nConfigure the Camera component to use the physical Camera property. \nMatch the sensor size with the camera resolution and configure the FoV/FocalLength."), Space]
    [SerializeField] private Texture2D ColorImage;
    private bool hasFirstFrameReceived;
    private Texture2D textureSource;

    [Header("Resulting View (leave empty)")]
    public RenderTexture ViewRenderTexture;
    private Camera _Camera;
    private Material RealVirtualMergeMaterial;

    #region Image Variables
    // The offset of two pixel increases stability during compensation
    public int Width
    {
        get
        {
            return _Width - 2;
        }
        private set
        {
            _Width = value;
        }
    }
    private int _Width;
    public int Height
    {
        get
        {
            return _Height - 2;
        }
        private set
        {
            _Height = value;
        }
    }
    private int _Height;
    public float FocalLength
    {
        get
        {
            return _Camera.focalLength * CompensationRatio;
        }
    }

    public float Ratio
    {
        get
        {
            return CompensationRatio;
        }
    }
    private float CompensationRatio = 1;
    #endregion

    [Space]
    public PBM_CameraFrustum Frustum;

    private void Awake()
    {
        try
        {
            subscriber = new Subscriber(host, port);
            subscriber.AddTopicCallback("Size", data => OnColorSizeReceived(data));
            subscriber.AddTopicCallback("Color", data => OnColorFrameReceived(data));
            Debug.Log("Subscriber setup complete with host: " + host + " and port: " + port);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to start subscriber: " + e.Message);
        }

        _Camera = GetComponent<Camera>();
        _Camera.cullingMask &= ~(1 << LayerMask.NameToLayer("PBM"));
        _Camera.usePhysicalProperties = true;
        Width = (int)_Camera.sensorSize.x;
        Height = (int)_Camera.sensorSize.y;
        _Camera.aspect = 1.0f * _Width / _Height;

        RealVirtualMergeMaterial = new Material(Shader.Find("PBM/ViewMerge"));

        ViewRenderTexture = new RenderTexture(_Width, _Height, 24);
        ViewRenderTexture.name = "PBMView";
        ViewRenderTexture.Create();

        Frustum.Create(LayerMask.NameToLayer("PBM"), transform);

        hasFirstFrameReceived = false;
    }

    private void OnColorSizeReceived(byte[] data)
    {
        if(data.Length != 2 * sizeof(int))
        {
            Debug.LogError($"PBM_CaptureCamera::OnColorSizeReceived(): Data length is not right");
            return;
        }

        int[] sizeArray = new int[2];
        Buffer.BlockCopy(data, 0, sizeArray, 0, data.Length);
        int width = sizeArray[0];
        int height = sizeArray[1];

        Debug.Log($"Recieve width is {width}, height is {height}");
        Debug.Log($"_Camera width is {Width}, height is {Height}");

        UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
        {
            if (ColorImage == null)
            {
                ColorImage = new Texture2D(width, height, TextureFormat.RGB24, false);
                Debug.Log($"PBM_CaptureCamera::OnColorSizeReceived(): Initialized new ColorImage with width: {width}, height: {height}");
            }
        });
    }

    private void OnColorFrameReceived(byte[] data)
    {
        hasFirstFrameReceived |= true;

        UnityMainThreadDispatcher.Dispatcher.Enqueue(() => 
        {
            if (ColorImage != null )
            {
                ColorImage.LoadRawTextureData(data);
                ColorImage.Apply();

                Debug.Log($"OnColorFrameReceived: ColorImage has been updated. Type: {ColorImage.GetType()}");
            }
        });
    }

    public void UpdateValidAreaCompensationWithObserver(Vector3 ObserverWorldPos)
    {
        CompensationRatio = GetCompensationRatio(ObserverWorldPos);
    }

    private void LateUpdate()
    {
        _Camera.Render();

        Frustum.UpdateFrustum(_Camera.focalLength, _Camera.sensorSize.x, _Camera.sensorSize.y);
    }

    private bool IsValidObserverPosition(Vector3 worldPos)
    {
        var vfov = _Camera.fieldOfView * Mathf.Deg2Rad;
        var hfov = 2 * Mathf.Atan( Mathf.Tan(vfov / 2) * _Camera.aspect);

        var a = Mathf.Tan(hfov / 2);
        var b = Mathf.Tan(vfov / 2);

        var pointInCameraCoord = _Camera.transform.InverseTransformPoint(worldPos);
        pointInCameraCoord.z = 0;

        var angle = Mathf.Min(
            Mathf.Abs(Vector3.Angle(Vector3.right, pointInCameraCoord)),
            Mathf.Abs(Vector3.Angle(Vector3.left, pointInCameraCoord)));
   
        var phi = angle * Mathf.Deg2Rad;

        var gamma = 2 * Mathf.Atan(Mathf.Cos(phi) * a + Mathf.Sin(phi) * b);

        var theta_critical = 180 - gamma * Mathf.Rad2Deg;

        var angleObjectToForward = Vector3.Angle(
            _Camera.transform.InverseTransformPoint(worldPos),
            Vector3.forward);

        return angleObjectToForward < theta_critical / 2;
    }

    private float GetCompensationRatio(Vector3 ObserverWorldpos)
    {
        float ratio = 1;
        if (!IsValidObserverPosition(ObserverWorldpos))
        {
            var pointInCamera = _Camera.transform.InverseTransformPoint(ObserverWorldpos);
            pointInCamera.z = 0;

            var angle = Mathf.Min(
                    Mathf.Abs(Vector3.Angle(Vector3.right, pointInCamera)),
                    Mathf.Abs(Vector3.Angle(Vector3.left, pointInCamera)));

            var phi = angle * Mathf.Deg2Rad;

            var fov_xy = GetCameraFovForValidPBM(ObserverWorldpos).y;
            var radVFOV0 = fov_xy * Mathf.Deg2Rad;

            var f_Y = 
                Mathf.Sin(phi) * (_Height / (2 * Mathf.Tan(radVFOV0 / 2))) +
                Mathf.Cos(phi) * (_Width / (2 * Mathf.Tan(radVFOV0 / 2)));

            ratio = f_Y / _Camera.focalLength;

        }
        return ratio;
    }

    private Vector2 GetCameraFovForValidPBM(Vector3 ObserverWorldpos)
    {
        var pointInCamera = _Camera.transform.InverseTransformPoint(ObserverWorldpos);

        var theta_critical = Vector3.Angle(pointInCamera, Vector3.forward) * 2 * Mathf.Deg2Rad;

        var fov = (Mathf.PI - theta_critical) * Mathf.Rad2Deg;

        return new Vector2(fov, fov);
    }

    // https://stackoverflow.com/questions/44264468/convert-rendertexture-to-texture2d
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (ColorImage != null && hasFirstFrameReceived)
        {
            if(textureSource == null)
            {
                textureSource = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            }
            RenderTexture.active = source;
            textureSource.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            textureSource.Apply();

            RealVirtualMergeMaterial.mainTexture = textureSource;
            RealVirtualMergeMaterial.SetTexture("_RealContentTex", ColorImage);

            Debug.Log($"OnRenderImage: textureSource is {textureSource}, " +
                $"ColorImage size is {ColorImage.width} {ColorImage.height}, " +
                $"ViewRenderTexture size is {ViewRenderTexture.width} {ViewRenderTexture.height}");

            Graphics.Blit(source, ViewRenderTexture, RealVirtualMergeMaterial);
        }
    }


    //private void OnRenderImage(RenderTexture source, RenderTexture destination)
    //{
    //    if (ColorImage != null && hasFirstFrameReceived)
    //    {
    //        Resize(ColorImage, source.width, source.height);

    //        RealVirtualMergeMaterial.mainTexture = source;
    //        RealVirtualMergeMaterial.SetTexture("_RealContentTex", ColorImage);

    //        Debug.Log($"OnRenderImage: source is {source}, " +
    //            $"ColorImage size is {ColorImage.width} {ColorImage.height}, " +
    //            $"ViewRenderTexture size is {ViewRenderTexture.width} {ViewRenderTexture.height}");

    //        Graphics.Blit(source, ViewRenderTexture, RealVirtualMergeMaterial);
    //    }
    //}

    // https://github.com/ababilinski/unity-gpu-texture-resize/blob/master/ResizeTool.cs
    private void Resize(Texture2D texture2D, int targetX, int targetY, bool mipmap = true, FilterMode filter = FilterMode.Bilinear)
    {
        //create a temporary RenderTexture with the target size
        RenderTexture rt = RenderTexture.GetTemporary(targetX, targetY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);

        //set the active RenderTexture to the temporary texture so we can read from it
        RenderTexture.active = rt;

        //Copy the texture data on the GPU - this is where the magic happens [(;]
        Graphics.Blit(texture2D, rt);
        //resize the texture to the target values (this sets the pixel data as undefined)
        texture2D.Resize(targetX, targetY, texture2D.format, mipmap);
        texture2D.filterMode = filter;

        try
        {
            //reads the pixel values from the temporary RenderTexture onto the resized texture
            texture2D.ReadPixels(new Rect(0.0f, 0.0f, targetX, targetY), 0, 0);
            //actually upload the changed pixels to the graphics card
            texture2D.Apply();
        }
        catch
        {
            Debug.LogError("Read/Write is not enabled on texture " + texture2D.name);
        }


        RenderTexture.ReleaseTemporary(rt);
    }
}
