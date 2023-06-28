using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[InternalBufferCapacity(2)]
public struct GoalKeyList : IBufferElementData
{
    public int key;
}
