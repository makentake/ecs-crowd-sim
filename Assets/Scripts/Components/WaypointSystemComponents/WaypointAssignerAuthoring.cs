using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct WaypointAssigner : IComponentData
{
    public Entity goal;
}

public class WaypointAssignerAuthoring : MonoBehaviour
{
	public GameObject goal;
}