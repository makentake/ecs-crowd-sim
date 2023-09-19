using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct AgentCount : IComponentData
{
    public int count;
}

public class AgentCountAuthoring : MonoBehaviour
{
	public int count;
}