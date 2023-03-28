using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

[GenerateAuthoringComponent]
public struct RioterSpawner : IComponentData
{
    public Random random;
    public Entity goal, exit;
    public Entity agent, antifa;
    public float3 spawnRadius;
    public int crowdSize, antifaSize;
    public int spawned, toSpawn;
    public bool done;
}
