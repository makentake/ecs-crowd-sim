using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct VoxelSpawner : IComponentData
{
    public float x, y;
    public float voxelSpacing;
    public Entity waypoint;
}

public class VoxelSpawnerAuthoring : MonoBehaviour
{
	public float x, y;
	public float voxelSpacing;
	public GameObject waypoint;
}