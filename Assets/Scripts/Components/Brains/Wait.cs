using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct Wait : IComponentData
{
    public float maxTime, elapsedTime;
}
