using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using TMPro;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class UISystem : SystemBase
{
    private EntityQuery agentQuery, policeQuery;

    protected override void OnUpdate()
    {
        agentQuery = GetEntityQuery(typeof(Agent));
        policeQuery = GetEntityQuery(typeof(Police));

        Entities.ForEach((MonoUI t, ref AgentCount a) =>
        {
            a.count = agentQuery.CalculateEntityCount() - policeQuery.CalculateEntityCount();

            t.txt.text = "Agent #: " + a.count;
        }).WithoutBurst().Run();
    }
}
