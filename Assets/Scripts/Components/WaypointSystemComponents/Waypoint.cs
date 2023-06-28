using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

[GenerateAuthoringComponent]
public struct Waypoint : IComponentData
{
    public int key;
}
