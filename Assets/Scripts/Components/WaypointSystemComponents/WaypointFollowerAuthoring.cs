using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct WaypointFollower : IComponentData
{
    public int weight; 
    public int goalKey, startKey;
    public float lastSavedMinimum;
}

public class WaypointFollowerAuthoring : MonoBehaviour
{
	public int weight;
	public int goalKey, startKey;
	public float lastSavedMinimum;
}