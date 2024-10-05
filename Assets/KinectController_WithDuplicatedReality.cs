using PubSub;
using UnityEngine;

public class KinectController_WithDuplicatedReality : KinectSubscriber
{

    [Header("Duplicated Reality")]
    public bool EnableDuplication = true;
    public Transform RegionOfInterest;
    public Transform DuplicatedReality;
}