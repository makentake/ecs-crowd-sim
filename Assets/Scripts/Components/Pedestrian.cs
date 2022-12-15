using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct Pedestrian : IComponentData
{
    public float minDist,
        maxDist,
        speed,
        rotSpeed,
        targetFac,
        attractionFac,
        attractionRot,
        repulsionFac,
        obstacleFac,
        baseTolerance,
        tolerance;
    public quaternion heading;
    public float3 target, attraction, repulsion, obstacle;
    public int attractors, repellors;
}
