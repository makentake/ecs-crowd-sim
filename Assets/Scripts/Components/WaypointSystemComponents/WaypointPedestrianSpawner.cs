using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

[GenerateAuthoringComponent]
public struct WaypointPedestrianSpawner : IComponentData
{
    // Visible variables
    public Entity agent;
    public float3 spawnRadius;
    public int toSpawn;

    // Navigation
    public int navigationWeight;
    public float maxRecalculationTime, minDensity, maxDensity;

    public float percentYoung, percentWaiting;

    // Internal variables
    public Random random;
    public int spawned;
    public bool done;
}
