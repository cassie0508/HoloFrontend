using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PBM_CaptureCamera : MonoBehaviour
{

    [Header("Feed the camera texture into ColorImage. \nConfigure the Camera component to use the physical Camera property. \nMatch the sensor size with the camera resolution and configure the FoV/FocalLength."), Space]
    public Texture ColorImage;

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

   

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RealVirtualMergeMaterial.mainTexture = source;

        RealVirtualMergeMaterial.SetTexture("_RealContentTex", ColorImage);

        Graphics.Blit(source, ViewRenderTexture, RealVirtualMergeMaterial);
    }


}
