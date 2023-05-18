using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Timer : MonoBehaviour
{
    private float elapsedTime = 0;
    private float maxTime = 600;

    // Update is called once per frame
    void Update()
    {
        if (elapsedTime < maxTime)
        {
            elapsedTime += Time.deltaTime;
        }
        else
        {
            //Debug.Break();
        }
    }
}
