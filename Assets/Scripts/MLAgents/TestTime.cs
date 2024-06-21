using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class TestTime : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var pms = World.DefaultGameObjectInjectionWorld.GetExistingSystem<PedestrianMovementSystem>();
        //World.DefaultGameObjectInjectionWorld.GetExistingSystem<GraphConnectionSystem>().onDemand = true;

        //Debug.Log(pms.elapsedTime);

        if (pms.elapsedTime >= 60f || (pms.rewards.IsCreated && pms.rewards.Length >= 600))
        {
            float totalReward = 0f;

            foreach (var reward in pms.rewards)
            {
                totalReward += reward;
            }

            Debug.Log($"Total reward: {totalReward}, elapsed time: {pms.elapsedTime}");

            pms.rewards.Clear();
            pms.elapsedTime = 0f;

            EndEpisode();
        }
    }

    private void EndEpisode()
    {
        World.DefaultGameObjectInjectionWorld.GetExistingSystem<SpawningSystem>().finished = true;
    }
}
