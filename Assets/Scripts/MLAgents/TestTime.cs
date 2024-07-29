using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

public class TestTime : MonoBehaviour
{
    // stuff for averaging
    private List<float> resultsList;

    // Start is called before the first frame update
    void Start()
    {
        resultsList = new List<float>(32);
    }

    // Update is called once per frame
    void Update()
    {
        var pms = World.DefaultGameObjectInjectionWorld.GetExistingSystem<PedestrianMovementSystem>();
        //World.DefaultGameObjectInjectionWorld.GetExistingSystem<GraphConnectionSystem>().onDemand = true;

        //Debug.Log(pms.elapsedTime);

        if (pms.elapsedTime >= 60f)// || (pms.rewards.IsCreated && pms.rewards.Length >= 600))
        {
            float totalReward = 0f;

            foreach (var reward in pms.rewards)
            {
                totalReward += reward;
            }

            Debug.Log($"Total reward: {totalReward}, elapsed time: {pms.elapsedTime}");

            // stuff for averaging
            resultsList.Add(totalReward);

            if (resultsList.Count >= 32)
            {
                Debug.Log($"32 RUN AVERAGE: {resultsList.Average()}");
                Debug.Break();
            }

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
