using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

public struct PedestrianSpawner : IComponentData
{
    public Random random;
    public Entity goal, exit;
    public Entity agent;
    public float3 spawnRadius;
    public int crowdSize;
    public int spawned, toSpawn;
    public bool done;
}

public class PedestrianSpawnerAuthoring : MonoBehaviour
{
	public Random random;
	public GameObject goal, exit;
	public GameObject agent;
	public float3 spawnRadius;
	public int crowdSize;
	public int spawned, toSpawn;
	public bool done;
}