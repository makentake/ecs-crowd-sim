using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

// this system ensures deltaTime is corrected to 0 on frames where FixedUpdate doesn't run
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class DeltaTimeAdjusterSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var currentTime = GetSingleton<ElapsedTimeComponent>().elapsedTime;
        SetSingleton(new ElapsedTimeComponent
        {
            elapsedTime = currentTime,
            deltaTime = 0f
        });
    }
}
