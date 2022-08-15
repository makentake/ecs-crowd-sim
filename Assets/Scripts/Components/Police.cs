using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct Police : IComponentData
{
    public Entity interactionTarget;
    public float4 squadHeading;
    public float angerDefuse, radius;
}
