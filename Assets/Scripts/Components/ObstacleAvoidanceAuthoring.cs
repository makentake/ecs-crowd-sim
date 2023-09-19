using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct ObstacleAvoidance : IComponentData
{
    public int numberOfRays, visionAngle;
    public float movementPerRay, visionLength;
}

public class ObstacleAvoidanceAuthoring : MonoBehaviour
{
	public int numberOfRays, visionAngle;
	public float movementPerRay, visionLength;
}