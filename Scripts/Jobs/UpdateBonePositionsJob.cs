using UnityEngine;
using UnityEngine.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace KageKirin.SpheroidNormal
{
    [BurstCompile]
    public struct UpdateBonePositionsJob : IJobParallelForTransform
    {
        [ReadOnly]
        public float4x4            rootTransform;

        [WriteOnly]
        public NativeArray<float3> bonePositions;

        public void Execute(int i, TransformAccess transform)
        {
            bonePositions[i] = math.transform(rootTransform, transform.position);
        }
    }
} // namespace KageKirin.SpheroidNormal