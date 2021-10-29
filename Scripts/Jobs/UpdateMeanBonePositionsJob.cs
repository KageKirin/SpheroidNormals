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
    public struct UpdateMeanBonePositionsJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float3> refBonePositions;

        [ReadOnly]
        public NativeArray<int4>   boneIndices;

        [ReadOnly]
        public NativeArray<float4> boneWeights;

        [WriteOnly]
        public NativeArray<float3> outBonePositions;
        public void                Execute(int i)
        {
            int4   boneIndex    = boneIndices[i];
            float4 boneWeight   = boneWeights[i];

            Debug.Assert(boneIndex.x < refBonePositions.Length);
            Debug.Assert(boneIndex.y < refBonePositions.Length);
            Debug.Assert(boneIndex.z < refBonePositions.Length);
            Debug.Assert(boneIndex.w < refBonePositions.Length);

            outBonePositions[i] = refBonePositions[boneIndex.x] * boneWeight.x   //
                                + refBonePositions[boneIndex.y] * boneWeight.y   //
                                + refBonePositions[boneIndex.z] * boneWeight.z   //
                                + refBonePositions[boneIndex.w] * boneWeight.w;
        }
    }
}
