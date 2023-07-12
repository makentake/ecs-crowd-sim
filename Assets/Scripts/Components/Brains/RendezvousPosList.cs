using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

[InternalBufferCapacity(1)]
public struct RendezvousPosList : IBufferElementData
{
    public float3 pos;
}
