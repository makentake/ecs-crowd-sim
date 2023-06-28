using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[InternalBufferCapacity(6)]
public struct WaypointList : IBufferElementData
{
    public int key;
}
