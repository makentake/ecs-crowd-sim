using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct DensityAvoidanceBrain : IComponentData
{
    public float maxTime, elapsedTime;
    public float minDensityTolerance;
    public float maxDensityTolerance;
}
