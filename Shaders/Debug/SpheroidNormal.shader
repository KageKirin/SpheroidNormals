// This shader draws a texture on the mesh.
Shader "SpheroidNormal/Debug/SpheroidNormal"
{
    // The _BaseMap variable is visible in the Material's Inspector, as a field
    // called Base Map.
    Properties
    {
        [Toggle(SPHEROID_NORMAL_BUFFER_ON)]
        _EnableSpheroidNormals("Enable Spheroid Normals", Int) = 0

        [PerRendererData]
        _UsePassthroughMeanBonePosition("Use Mean Bone Position (instead of Computed one)", Int) = 0

        [PerRendererData]
        _NormalHull("Flag to render normal hull", Int) = 0

        [PerRendererData]
        _NormalHullScale("Scale for normal hull", Float) = 0.1

        _BaseMap("Base Map", 2D) = "white"
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalRenderPipeline"
        }

        Pass
        {
            HLSLPROGRAM
#pragma vertex   vert
#pragma fragment frag
#pragma multi_compile __ SPHEROID_NORMAL_BUFFER_ON  //!< multi_compile: we explicitly want both variants


#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                // The uv variable contains the UV coordinate on the texture for the
                // given vertex.
                float2 uv : TEXCOORD0;

                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                // The uv variable contains the UV coordinate on the texture for the
                // given vertex.
                float2 uv : TEXCOORD0;
                float3 computedMeanBonePosition : TEXCOORD1;
                float3 passthroughMeanBonePosition : TEXCOORD2;
            };

            // This macro declares _BaseMap as a Texture2D object.
            TEXTURE2D(_BaseMap);
            // This macro declares the sampler for the _BaseMap texture.
            SAMPLER(sampler_BaseMap);

            int _UsePassthroughMeanBonePosition;
            int _NormalHull;
            float _NormalHullScale;

            CBUFFER_START(UnityPerMaterial)
            // The following line declares the _BaseMap_ST variable, so that you
            // can use the _BaseMap variable in the fragment shader. The _ST
            // suffix is necessary for the tiling and offset function to work.
            float4 _BaseMap_ST;
            CBUFFER_END

#if SPHEROID_NORMAL_BUFFER_ON
            StructuredBuffer<float3> MeanBonePositions;
            StructuredBuffer<float3> BonePositions;
            StructuredBuffer<int4>   BoneIndices;
            StructuredBuffer<float4> BoneWeights;
#endif // SPHEROID_NORMAL_BUFFER_ON

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

#if SPHEROID_NORMAL_BUFFER_ON
                int4   boneIndices = BoneIndices[IN.vertexID];
                float4 boneWeights = BoneWeights[IN.vertexID];

                float3 passthroughMeanBonePosition = MeanBonePositions[IN.vertexID];
                float3 computedMeanBonePosition    = BonePositions[int(boneIndices.x)] * boneWeights.x  //
                                                    + BonePositions[int(boneIndices.y)] * boneWeights.y  //
                                                    + BonePositions[int(boneIndices.z)] * boneWeights.z  //
                                                    + BonePositions[int(boneIndices.w)] * boneWeights.w;

                OUT.passthroughMeanBonePosition = passthroughMeanBonePosition;
                OUT.computedMeanBonePosition    = computedMeanBonePosition;

                float3 positionOS = IN.positionOS.xyz;

                if (_NormalHull)
                {
                    float3 meanBonePosition = computedMeanBonePosition;
                    positionOS = positionOS + normalize(positionOS - meanBonePosition) * _NormalHullScale;
                }

                OUT.positionHCS = TransformObjectToHClip(positionOS);
#else // !SPHEROID_NORMAL_BUFFER_ON
                OUT.passthroughMeanBonePosition = float3(0,0,0);
                OUT.computedMeanBonePosition = float3(0,0,0);
#endif // SPHEROID_NORMAL_BUFFER_ON

                // The TRANSFORM_TEX macro performs the tiling and offset
                // transformation.
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (_UsePassthroughMeanBonePosition)
                {
                    return half4(IN.passthroughMeanBonePosition, 1);
                }
                return half4(IN.computedMeanBonePosition, 1);
            }
            ENDHLSL
        }
    }
}
