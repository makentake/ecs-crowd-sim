using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct WaypointDensity : IComponentData
{
    public float range;
    public int maxAgents;
    public int currentAgents;
}

public class WaypointDensityAuthoring : MonoBehaviour
{
	public float range;
	public int maxAgents;
	public int currentAgents;
}