using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

public struct Waypoint : IComponentData
{
    public int key;
}

public class WaypointAuthoring : MonoBehaviour
{
	public int key;
}