using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraPositioner : MonoBehaviour
{
    public Transform playerCam;
    public Transform orientation;

    private void Update()
    {
        Debug.DrawRay(playerCam.position, playerCam.forward * 2, Color.red);
    }
}
