using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

[UpdateBefore(typeof(CrowdMovementSystem))]
public partial class PoliceMovementSystem : SystemBase
{
    //private EndFixedStepSimulationEntityCommandBufferSystem end;
    private EntityQuery policeQuery;

    // Start is called before the first frame update
    protected override void OnStartRunning()
    {

        Entities.WithStoreEntityQueryInField(ref policeQuery).WithAll<Police>().ForEach((ref Goal g, in Translation t) =>
        {
            g.goal = t;
        }).ScheduleParallel();
    }

    // Update is called once per frame
    protected override void OnUpdate()
    {
        var dt = Time.DeltaTime;

        Entities
            .WithAll<Police>()
            .ForEach((int entityInQueryIndex, ref Agent a, in Translation t, in Rotation r, in Goal g) =>
            {
                //float3 avoidance = v.obstacle;
                float3 avoidance = math.INFINITY;

                if (avoidance.x != math.INFINITY)
                {
                    a.target = new float3(avoidance.x, 0, avoidance.z);
                }
                else
                {
                    a.target = g.goal.Value - t.Value;
                }
            }).ScheduleParallel();

        Entities
            .ForEach((int entityInQueryIndex, ref PhysicsVelocity v, ref Agent a, ref Translation t, ref Rotation r, in Police p, in Goal g) =>
            {
                float3 target = a.target,
                repulsion = a.repulsion;
                float dist = math.distance(t.Value, g.goal.Value);

                target = target.x == 0 && target.y == 0 && target.z == 0 ? target : math.normalize(target);

                float3 final = target * a.targetFac;

                bool isZero = final.x == 0 && final.y == 0 && final.z == 0;

                final = isZero ? final : math.normalize(final);

                t.Value -= math.float3(0, t.Value.y - 1.5f, 0);
                r.Value.value.x = p.squadHeading.x;
                r.Value.value.y = p.squadHeading.y;
                r.Value.value.z = p.squadHeading.z;
                v.Angular = 0;

                if (dist > 0.1)
                {
                    v.Linear = final * a.speed;
                }
                else
                {
                    v.Linear = math.float3(0, 0, 0);
                }
            }).ScheduleParallel();
    }
}
