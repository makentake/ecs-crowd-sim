using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct WaypointDensity : IComponentData
{
    public float range;
    public int maxAgents;
    public int currentAgents;
}
