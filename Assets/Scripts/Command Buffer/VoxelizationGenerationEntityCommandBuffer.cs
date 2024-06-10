using Unity.Entities;
using Unity.Transforms;

//[UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
[UpdateBefore(typeof(GraphConnectionSystem))]
[UpdateAfter(typeof(VoxelSpawningSystem))]
public class VoxelizationGenerationEntityCommandBuffer : EntityCommandBufferSystem
{

}
