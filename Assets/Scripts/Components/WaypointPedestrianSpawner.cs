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
    public int crowdSize;
    public int toSpawn;

    // Internal variables
    public Random random;
    public int spawned;
    public bool done;
}
