using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct WaitTag : IComponentData
{
    public float maxTime, currentTime;
}
