using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
public partial class VariableRateManagerSystem : SystemBase
{
    protected override void OnCreate()
    {
        var rateManager = new RateUtils.VariableRateManager(17
            //*((uint)UnityEngine.Time.timeScale)
            );
        var variableRateSystem = World.GetExistingSystem<VariableRateSimulationSystemGroup>();
        variableRateSystem.RateManager = rateManager;
    }

    protected override void OnUpdate()
    {
        //Debug.Log("deltaTime: " + Time.DeltaTime);
    }
}
