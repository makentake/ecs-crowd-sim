using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct Goal : IComponentData
{
    public Translation goal;
    public Translation exit;
}

public class GoalAuthoring : MonoBehaviour
{
	public Translation goal;
	public Translation exit;
}