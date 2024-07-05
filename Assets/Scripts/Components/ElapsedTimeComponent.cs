using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct ElapsedTimeComponent : IComponentData
{
    public float elapsedTime;
    public float deltaTime;
}
