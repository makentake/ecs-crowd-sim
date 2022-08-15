using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[UpdateBefore(typeof(PoliceMovementSystem))]
[UpdateAfter(typeof(CrowdTaggingSystem))]
public class PreMovementEntityCommandBuffer : EntityCommandBufferSystem {}
