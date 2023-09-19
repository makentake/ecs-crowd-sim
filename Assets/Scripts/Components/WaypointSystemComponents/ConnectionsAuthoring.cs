using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[InternalBufferCapacity(8)]
public struct Connections : IBufferElementData
{
    public int key;
}

public class ConnectionsAuthoring : MonoBehaviour
{
	public int key;
}