using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct ObstacleAvoidance : IComponentData
{
    public int numberOfRays, visionAngle;
    public float movementPerRay, visionLength;
}
