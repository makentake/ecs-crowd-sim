using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
[InternalBufferCapacity(8)]
public struct BarricadeConnections : IBufferElementData
{
    public int key;
}
