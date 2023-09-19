using Unity.Entities;

using UnityEngine;

public struct Rioter : IComponentData
{
    public int policeRepulsion, threshhold;
    public float aggression;
}

public class RioterAuthoring : MonoBehaviour
{
	public int policeRepulsion, threshhold;
	public float aggression;
}