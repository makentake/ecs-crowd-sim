using Unity.Entities;

using UnityEngine;

public struct CrowdAreaCounter : IComponentData{
    public float lastCount;
    public float minX;
    public float maxX;
    public float minZ;
    public float maxZ;
    public int currentCount;
}

public class CrowdAreaCounterAuthoring : MonoBehaviour
{
	public float lastCount;
	public float minX;
	public float maxX;
	public float minZ;
	public float maxZ;
	public int currentCount;
}