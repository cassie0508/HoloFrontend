using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Webcam : MonoBehaviour
{
    public int WebcamIndex = 0;

    void Start()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        for (int i = 0; i < devices.Length; i++)
        {
            print(i + ": Webcam available: " + devices[i].name);
        }

        WebCamTexture tex = new WebCamTexture(devices[WebcamIndex].name);

        var capturingCamera = FindObjectOfType<PBM_CaptureCamera>();
        if (capturingCamera) capturingCamera.ColorImage = tex;
        tex.Play();
    }

}
