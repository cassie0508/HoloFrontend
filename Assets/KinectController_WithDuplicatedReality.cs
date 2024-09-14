using PubSub;
using UnityEngine;

public class KinectController_WithDuplicatedReality : KinectSubscriberCopy
{

    [Header("Duplicated Reality")]
    public bool EnableDuplication = true;
    public Transform RegionOfInterest;
    public Transform DuplicatedReality;
    public Transform HololensCamera; // User view

    public override void OnSetPointcloudProperties(Material pointcloudMat)
    {
        if(EnableDuplication) pointcloudMat.EnableKeyword("_DUPLICATE_ON");
        else pointcloudMat.DisableKeyword("_DUPLICATE_ON");

        pointcloudMat.SetMatrix("_Roi2Dupl", DuplicatedReality.localToWorldMatrix * RegionOfInterest.worldToLocalMatrix);
        pointcloudMat.SetMatrix("_ROI_Inversed", RegionOfInterest.worldToLocalMatrix);
        pointcloudMat.SetMatrix("_Dupl_Inversed", DuplicatedReality.worldToLocalMatrix);
    }

    protected override void Update()
    {
        if (Camera.main != null)
        {
            HololensCamera = Camera.main.transform;
        }
        base.Update();
    }
}
