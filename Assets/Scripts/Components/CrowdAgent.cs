using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[GenerateAuthoringComponent]
public struct CrowdAgent : IComponentData
{
    public float minDist,
        maxDist,
        speed,
        rotSpeed,
        targetFac,
        attractionFac,
        attractionRot,
        repulsionFac,
        baseTolerance, 
        tolerance;
    public quaternion heading;
    public float3 target, attraction, repulsion, obstacle;
    public int attractors, repellors;
}
