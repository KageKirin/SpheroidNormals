using UnityEngine;
using UnityEngine.Jobs;
using UnityEditor;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace KageKirin.SpheroidNormal
{
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class SpheroidNormalController : MonoBehaviour
    {
        [Header("Shader Control")]
        public bool  _normalHull      = false;
        public float _normalHullScale = 0.0f;


        [Header("made public for debug")]
        // instance data
        public SkinnedMeshRenderer   _skinnedMeshRenderer;
        public MaterialPropertyBlock _materialPropertyBlock;

        // per-vertex bone indices
        public ComputeBuffer _boneIndexBuffer;
        NativeArray<int4>    _boneIndices;

        // per-vertex bone weights
        public ComputeBuffer _boneWeightBuffer;
        NativeArray<float4>  _boneWeights;

        // per-vertex 'mean' bone position
        // this is the result of the weighted sum of the per-vertex bones
        public ComputeBuffer       _meanBonePositionBuffer;
        public NativeArray<float3> _meanBonePositions;

        // per-instance bone positions in object space (OS)
        public ComputeBuffer       _bonePositionBuffer;
        public NativeArray<float3> _bonePositions;

        // job accessor to transforms
        public TransformAccessArray _transformAccessArray;

        // job to update the per-instance _bonePositions from the current animation state
        UpdateBonePositionsJob _updateBonePositionsJob;
        JobHandle              _updateBonePositionsJobHandle;

        [Header("internal state -- DON'T TOUCH")]
        public bool _initComplete = false;

        [Header("DEBUG")]
        public int _initCallCounter = 0;
        public int _releaseCallCounter = 0;


#region construct and init
        void Start()
        {
            // enable global shader state
            Shader.EnableKeyword("SPHEROID_NORMAL_BUFFER_ON");
        }

        // OnEnable() | OnDisable() called when script becomes enabled|disabled
        void OnEnable()
        {
            Debug.Log($"{name}.OnEnable()");
            InitializeFromClean();
        }

        void InitializeFromClean()
        {
            if (_initComplete)
            {
                AssertInternalState(true);
                return;
            }

            ReleaseEverything();

            if (_skinnedMeshRenderer == null)
            {
                var smr = gameObject.GetComponent<SkinnedMeshRenderer>();
                Initialize(smr);
            }
            else
            {
                AssertInternalState(true);
            }
        }

        void Initialize(SkinnedMeshRenderer smr)
        {
            _initCallCounter++;
            Debug.Log($"{name}.Initialize() was called {_initCallCounter} times");

            AssertInternalState(false);

            Debug.Assert(smr != null);
            Debug.Assert(smr.sharedMesh != null);

            _skinnedMeshRenderer = smr;

            InitializeMeshBuffers();
            InitializeBoneBuffers();
            InitializeMaterialBlock();

            AssertInternalState(true);
        }

        void InitializeMeshBuffers()
        {
            var mesh     = _skinnedMeshRenderer.sharedMesh;
            _boneIndices = new NativeArray<int4>(mesh.vertexCount, Allocator.Persistent);
            _boneWeights = new NativeArray<float4>(mesh.vertexCount, Allocator.Persistent);

            for (int idx = 0; idx < mesh.vertexCount; idx++)
            {
                BoneWeight bw = mesh.boneWeights[idx];

                _boneIndices[idx] = new int4(bw.boneIndex0, bw.boneIndex1, bw.boneIndex2, bw.boneIndex3);
                _boneWeights[idx] = new float4(bw.weight0, bw.weight1, bw.weight2, bw.weight3);
            }

            _boneIndexBuffer = new ComputeBuffer(mesh.vertexCount, 4 * sizeof(int));
            _boneIndexBuffer.SetData(_boneIndices);

            _boneWeightBuffer = new ComputeBuffer(mesh.vertexCount, 4 * sizeof(float));
            _boneWeightBuffer.SetData(_boneWeights);
        }

        void InitializeBoneBuffers()
        {
            var bones           = _skinnedMeshRenderer.bones;
            _bonePositions      = new NativeArray<float3>(bones.Length, Allocator.Persistent);
            _bonePositionBuffer = new ComputeBuffer(bones.Length, sizeof(float) * 3);
            _bonePositionBuffer.SetData(_bonePositions);

            _transformAccessArray = new TransformAccessArray(bones.Length);
            _transformAccessArray.SetTransforms(bones);
        }

        void InitializeMaterialBlock()
        {
            // create material property block
            if (_materialPropertyBlock == null)
            {
                _materialPropertyBlock = new MaterialPropertyBlock();
            }

            if (_skinnedMeshRenderer.HasPropertyBlock())
            {
                _skinnedMeshRenderer.GetPropertyBlock(_materialPropertyBlock);
            }
            _materialPropertyBlock.SetBuffer("BoneIndices", _boneIndexBuffer);
            _materialPropertyBlock.SetBuffer("BoneWeights", _boneWeightBuffer);
            _materialPropertyBlock.SetBuffer("BonePositions", _bonePositionBuffer);

            _initComplete = true;
            _materialPropertyBlock.SetInteger("EnableSpheroidNormals", _initComplete ? 1 : 0);
            _materialPropertyBlock.SetInteger("NormalHull", _normalHull ? 1 : 0);
            _materialPropertyBlock.SetFloat("NormalHullScale", _normalHullScale);

            _skinnedMeshRenderer.SetPropertyBlock(_materialPropertyBlock);
        }

        private void AssertInternalState(bool allocationState)
        {
            if (allocationState)
            {
                Debug.Assert(_boneIndexBuffer != null && _boneIndexBuffer.IsValid());
                Debug.Assert(_boneWeightBuffer != null && _boneWeightBuffer.IsValid());
                Debug.Assert(_bonePositionBuffer != null && _bonePositionBuffer.IsValid());

                Debug.Assert(_boneIndices != null && _boneIndices.IsCreated);
                Debug.Assert(_boneWeights != null && _boneWeights.IsCreated);
                Debug.Assert(_bonePositions != null && _bonePositions.IsCreated);

                Debug.Assert(_transformAccessArray.isCreated);
                Debug.Assert(_materialPropertyBlock != null);
                Debug.Assert(_skinnedMeshRenderer != null);
            }
            else
            {
                Debug.Assert(_boneIndexBuffer == null || !_boneIndexBuffer.IsValid());
                Debug.Assert(_boneWeightBuffer == null || !_boneWeightBuffer.IsValid());
                Debug.Assert(_bonePositionBuffer == null || !_bonePositionBuffer.IsValid());

                Debug.Assert(_boneIndices == null || !_boneIndices.IsCreated);
                Debug.Assert(_boneWeights == null || !_boneWeights.IsCreated);
                Debug.Assert(_bonePositions == null || !_bonePositions.IsCreated);

                Debug.Assert(!_transformAccessArray.isCreated);
                Debug.Assert(_materialPropertyBlock == null);
                Debug.Assert(_skinnedMeshRenderer == null);
            }
        }
#endregion // construct and init


#region janitoring
        // mirroring init in OnEnable()
        void OnDisable()
        {
            Debug.Log($"{name}.OnDisable()");
            ReleaseEverything();
            //AssertInternalState(false);
        }

        private void ReleaseEverything()
        {
            _releaseCallCounter++;
            Debug.Log($"{name}.ReleaseEverything() was called {_releaseCallCounter} times");

            _updateBonePositionsJobHandle.Complete();

            // destroy bone indices
            if (_boneIndices != null && _boneIndices.IsCreated)
            {
                _boneIndices.Dispose();
            }

            if (_boneIndexBuffer != null && _boneIndexBuffer.IsValid())
            {
                _boneIndexBuffer.Dispose();
            }

            // destroy bone weights
            if (_boneWeights != null && _boneWeights.IsCreated)
            {
                _boneWeights.Dispose();
            }

            if (_boneWeightBuffer != null && _boneWeightBuffer.IsValid())
            {
                _boneWeightBuffer.Dispose();
            }

            // destroy bone positions
            if (_bonePositions != null && _bonePositions.IsCreated)
            {
                _bonePositions.Dispose();
            }

            if (_bonePositionBuffer != null && _bonePositionBuffer.IsValid())
            {
                _bonePositionBuffer.Dispose();
            }

            // destroy transform accessor
            if (_transformAccessArray.isCreated)
            {
                _transformAccessArray.Dispose();
            }

            _initComplete = false;
        }
#endregion // janitoring

#region update cycle
        void Update()
        {
            if (_bonePositions != null && _bonePositions.IsCreated)
            {
                _updateBonePositionsJob = new UpdateBonePositionsJob() {
                    bonePositions = _bonePositions,
                    rootTransform = gameObject.transform.worldToLocalMatrix,
                };
                _updateBonePositionsJobHandle = _updateBonePositionsJob.Schedule(_transformAccessArray);
            }

            if (_materialPropertyBlock != null)
            {
                _skinnedMeshRenderer.GetPropertyBlock(_materialPropertyBlock);
                _materialPropertyBlock.SetInteger("_NormalHull", _normalHull ? 1 : 0);
                _materialPropertyBlock.SetFloat("_NormalHullScale", _normalHullScale);
                _skinnedMeshRenderer.SetPropertyBlock(_materialPropertyBlock);
            }
        }

        void LateUpdate()
        {
            // ensure job completion
            _updateBonePositionsJobHandle.Complete();
            if (_bonePositionBuffer != null && _bonePositions != null)
            {
                //_computeBuffer.SetData(_bonePositions);
                _bonePositionBuffer.SetData(_updateBonePositionsJob.bonePositions);
            }

            if (_materialPropertyBlock != null)
            {
                _skinnedMeshRenderer.GetPropertyBlock(_materialPropertyBlock);
                _materialPropertyBlock.SetBuffer("BonePositions", _bonePositionBuffer);
                _skinnedMeshRenderer.SetPropertyBlock(_materialPropertyBlock);
            }

        }
#endregion // update cycle
    }
} // namespace KageKirin.SpheroidNormal
