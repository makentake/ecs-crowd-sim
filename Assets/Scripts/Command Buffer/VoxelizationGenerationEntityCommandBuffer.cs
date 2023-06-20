using Unity.Entities;

[UpdateBefore(typeof(GraphConnectionSystem))]
[UpdateAfter(typeof(VoxelSpawningSystem))]
public class VoxelizationGenerationEntityCommandBuffer : EntityCommandBufferSystem
{
    
}
