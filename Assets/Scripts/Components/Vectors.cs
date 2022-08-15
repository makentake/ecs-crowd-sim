using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct Vectors : IComponentData
{
    public float3 target, attraction, repulsion, obstacle;
    public int attractors, repellors;
}
