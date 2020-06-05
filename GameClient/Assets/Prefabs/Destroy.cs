using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Destroy : MonoBehaviour
{
    public float seconds = 5f;
    IEnumerator waitndestroy()
    {
        yield return new WaitForSeconds(seconds);
        GameObject.Destroy(transform.gameObject);
    }
    private void Awake()
    {
        StartCoroutine(waitndestroy());
    }
}
