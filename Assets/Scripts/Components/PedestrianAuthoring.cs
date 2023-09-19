using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public struct Pedestrian : IComponentData
{
    // Factors
    public float targetFac,
        attractionFac,
        repulsionFac,
        obstacleFac,
        lightFac;

    // Movement
    public float baseSpeed,
        rotSpeed;

    // Distances/tolerances
    public float baseMinDist,
        maxDist,
        wallTolerance,
        tolerance,
        lightRange,
        attractionRot,
        attractionSpeedTolerance,
        averageHeadingDuration;

    // Internal values
    // Local data
    public int densityModifier;
    public float speed, minDist;
    public bool isClimbing;

    // Attractor numbers
    public int lightAttractors;
    public int attractors, repellors;

    // Vectors
    public float3 target,
        attraction,
        repulsion,
        obstacle,
        lightAttraction;
}

public class PedestrianAuthoring : MonoBehaviour
{
	public float targetFac,
		attractionFac,
		repulsionFac,
		obstacleFac,
		lightFac;

	public float baseSpeed,
		rotSpeed;

	public float baseMinDist,
		maxDist,
		wallTolerance,
		tolerance,
		lightRange,
		attractionRot,
		attractionSpeedTolerance,
		averageHeadingDuration;

	public int densityModifier;
	public float speed, minDist;
	public bool isClimbing;
	public int lightAttractors;
	public int attractors, repellors;
	public float3 target,
		attraction,
		repulsion,
		obstacle,
		lightAttraction;

}