using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct AIBrain : IComponentData
{
    public bool isYoung;
    public bool willOccupy;
    public OccupationType occupationType;
}

public enum OccupationType
{
    rendezvous,
    idling,
    rioting
}
