using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct VoxelSpawner : IComponentData
{
    public float x, y;
    public float voxelSpacing;
    public Entity waypoint;
}
