using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using System.IO;

// A system for counting (and outputting) the crowd agents in the simulation
//[UpdateAfter(typeof(SpawningSystem))]
public partial class CrowdAreaCountingSystem : SystemBase {
    private NativeArray<int> count;
    private static float lastCountTime;

    protected override void OnStartRunning(){
        lastCountTime = (float)Time.ElapsedTime;
    }
    
    private partial struct CountJob : IJobEntity {
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Translation> targetArray;
        public NativeArray<int> count;

        public void Execute(ref CrowdAreaCounter counter){
            
            for(int i = 0; i<targetArray.Length; i++){
                Translation t = targetArray[i];

                //check if translation is within the area
                if (t.Value.x >= counter.minX && t.Value.x <= counter.maxX && t.Value.z >= counter.minZ && t.Value.z <= counter.maxZ)
                { 
                    count[0]++;
                }
            }
        }
    }

    private partial struct OutputCountJob : IJobEntity {
        public float time;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> count;

        public void Execute(ref CrowdAreaCounter counter){
            counter.currentCount = count[0];

            counter.lastCount = time;
            StreamWriter sw = new StreamWriter("crowdflowdata.txt", true);
            string toadd = time + "," + count[0];
            sw.WriteLine(toadd);
            sw.Close();
        }
    }

    protected override void OnUpdate(){
        float time = (float)Time.ElapsedTime;
        float frequency = 1f;
        //Debug.Log("Freq "+frequency+" diff " +(time - lastCountTime));
        if ((time - lastCountTime) > frequency)
        {
            lastCountTime = time;
            int[] countArray = { 0 };
            count = new NativeArray<int>(countArray, Allocator.TempJob);

            EntityQuery query = GetEntityQuery(ComponentType.ReadOnly<CrowdMemberTag>(), ComponentType.ReadOnly<Translation>());
            NativeArray<Translation> crowdTranslationArray = query.ToComponentDataArray<Translation>(Allocator.TempJob);

            JobHandle countJobHandle = new CountJob
            { // creates the counting job
                targetArray = crowdTranslationArray,
                count = count
            }.Schedule();

            JobHandle outputJobHandle = new OutputCountJob
            {
                time = time,
                count = count
            }.Schedule();
        }
    }
}