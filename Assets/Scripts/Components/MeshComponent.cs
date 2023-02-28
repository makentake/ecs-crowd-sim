using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public class MeshComponent : IComponentData
{
    public MeshRenderer mesh;
}
