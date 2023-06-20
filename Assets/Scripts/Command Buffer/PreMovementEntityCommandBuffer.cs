using Unity.Entities;

[UpdateBefore(typeof(PoliceMovementSystem))]
[UpdateAfter(typeof(CrowdTaggingSystem))]
public class PreMovementEntityCommandBuffer : EntityCommandBufferSystem {}
