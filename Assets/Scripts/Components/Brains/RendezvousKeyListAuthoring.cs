using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[InternalBufferCapacity(1)]
public struct RendezvousPointList : IBufferElementData
{
    public int key;
}
