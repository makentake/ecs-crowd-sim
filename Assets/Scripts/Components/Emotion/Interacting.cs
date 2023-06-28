using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

public struct Interacting : IComponentData 
{
    public float startingAnger;
    public Translation position;
}
