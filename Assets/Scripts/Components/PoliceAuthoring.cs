using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public struct Police : IComponentData
{
    public Entity interactionTarget;
    public float4 squadHeading;
    public float angerDefuse, radius;
}

public class PoliceAuthoring : MonoBehaviour
{
	public GameObject interactionTarget;
	public float4 squadHeading;
	public float angerDefuse, radius;
}