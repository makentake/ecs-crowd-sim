using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;

public partial class WaypointDensityCalculationSystem : SystemBase
{
    protected override void OnStartRunning()
    {
        Entities.ForEach((ref WaypointDensity d) =>
        {
            d.maxAgents = (int) math.ceil(math.PI * math.pow(d.range, 2));
        }).ScheduleParallel();
    }
    
    protected override void OnUpdate()
    {
        var pedestrianQuery = GetEntityQuery(ComponentType.ReadOnly<Pedestrian>(),
            ComponentType.ReadOnly<Translation>(),
            ComponentType.ReadOnly<Rotation>());

        var pedestrians = pedestrianQuery.ToComponentDataArray<Translation>(Allocator.TempJob);

        /*Entities.ForEach((ref WaypointDensity d) =>
        {
            d.maxAgents = (int)math.ceil(math.PI * math.pow(d.range, 2));
        }).ScheduleParallel();*/

        Entities
            .WithReadOnly(pedestrians)
            .ForEach((ref WaypointDensity d, in Translation t) =>
            {
                var precision = 4;
                d.currentAgents = 0;

                for (int i = 0; i < pedestrians.Length; i += precision)
                {
                    if (math.distance(t.Value, pedestrians[i].Value) <= d.range)
                    {
                        d.currentAgents++;
                    }
                }

                d.currentAgents *= precision;
                //d.currentAgents *= math.pow(d.currentAgents / d.maxAgents, 2);

                // enable this line to disable the system
                //d.currentAgents = 0;
            }).ScheduleParallel();

        pedestrians.Dispose(Dependency);
    }
}
