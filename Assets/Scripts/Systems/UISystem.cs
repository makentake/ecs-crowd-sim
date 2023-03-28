using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using TMPro;
using Unity.Collections;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class UISystem : SystemBase
{
    private EntityQuery agentQuery, pedestrianQuery, policeQuery;

    protected override void OnUpdate()
    {
        var count = new NativeArray<int>(1, Allocator.TempJob);

        Entities
            .ForEach((in CrowdAreaCounter c) =>
            {
                count[0] = c.currentCount;
            }).Schedule();

        /*agentQuery = GetEntityQuery(typeof(Agent));
        pedestrianQuery = GetEntityQuery(typeof(Pedestrian));
        policeQuery = GetEntityQuery(typeof(Police));*/

        Entities.ForEach((MonoUI t) =>
        {
            //a.count = pedestrianQuery.CalculateEntityCount() + 
            //agentQuery.CalculateEntityCount() - 
            //policeQuery.CalculateEntityCount();

            t.txt.text = "Agent #: " + count[0];
        }).WithoutBurst().Run();

        count.Dispose();
    }
}
