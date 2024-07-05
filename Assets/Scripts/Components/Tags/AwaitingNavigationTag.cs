using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct AwaitingNavigationTag : IComponentData
{
    public bool foundStart;
    public bool hasNavigated;
}
