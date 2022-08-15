using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

public struct InteractingTag : IComponentData 
{
    public float startingAnger;
    public Translation position;
}
