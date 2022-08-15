using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[GenerateAuthoringComponent]
public struct Goal : IComponentData
{
    public Translation goal;
    public Translation exit;
}
