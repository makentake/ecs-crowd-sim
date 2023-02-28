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
        tolerance,
        lightRange,
        lightAttractors,
        lightFac;
    public quaternion heading;
    public float3 target, 
        attraction, 
        repulsion, 
        obstacle, 
        lightAttraction;
    public int attractors, repellors;
    public bool isYoung, isClimbing;
}
