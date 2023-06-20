using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct WaypointFollower : IComponentData
{
    public int goalKey;
}
