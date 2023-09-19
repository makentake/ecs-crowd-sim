using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct Antifa : IComponentData
{
    public float instigation, radius;
}

public class AntifaAuthoring : MonoBehaviour
{
	public float instigation, radius;
}