using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(PoliceMovementSystem))]
public partial class CrowdTaggingSystem : SystemBase
{
    //private EndFixedStepSimulationEntityCommandBufferSystem end;
    private PreMovementEntityCommandBuffer pre;

    protected override void OnStartRunning()
    {
        //end = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        pre = World.GetOrCreateSystem<PreMovementEntityCommandBuffer>();
    }

    protected override void OnUpdate()
    {
        //var ecb = end.CreateCommandBuffer().AsParallelWriter();
        var ecb = pre.CreateCommandBuffer().AsParallelWriter();

        Entities
            .WithNone<Interacting>()
            .ForEach((Entity e, int entityInQueryIndex, ref Agent a, in Rioter r, in Translation t, in Goal g) =>
            {
                if (r.aggression > r.threshhold)
                {
                    if (math.distance(t.Value, g.goal.Value) < a.baseTolerance)
                    {
                        ecb.RemoveComponent<FleeingTag>(entityInQueryIndex, e);
                        ecb.RemoveComponent<MovingTag>(entityInQueryIndex, e);
                        ecb.AddComponent<RiotingTag>(entityInQueryIndex, e);
                        a.tolerance = -1;
                    }
                    else
                    {
                        ecb.RemoveComponent<FleeingTag>(entityInQueryIndex, e);
                        ecb.RemoveComponent<RiotingTag>(entityInQueryIndex, e);
                        ecb.AddComponent<MovingTag>(entityInQueryIndex, e);
                        a.tolerance = a.baseTolerance;
                    }
                }
                else
                {
                    ecb.RemoveComponent<RiotingTag>(entityInQueryIndex, e);
                    ecb.RemoveComponent<MovingTag>(entityInQueryIndex, e);
                    ecb.AddComponent<FleeingTag>(entityInQueryIndex, e);

                    //EntityManager.RemoveComponent<RiotingTag>(e);
                    //EntityManager.RemoveComponent<MovingTag>(e);
                    //EntityManager.AddComponent<FleeingTag>(e);

                    a.tolerance = a.baseTolerance;
                    //UnityEngine.Debug.Break();
                }
            }).ScheduleParallel();

        //end.AddJobHandleForProducer(Dependency);
        pre.AddJobHandleForProducer(Dependency);
    }
}
