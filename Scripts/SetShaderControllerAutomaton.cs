using System;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;

namespace KageKirin.SpheroidNormal
{
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class SetShaderControllerAutomaton : MonoBehaviour
    {
        [Header("Targets to search")]
        public Shader[] TargetedShaders;

        [Header("Controller components to assign")]
        public MonoScript[] _componentTypes;

#region startup
        void Start()
        {
            Refresh();
        }

#endregion // startup

#region update cycle
        void Update()
        {
            // Refresh();
        }
#endregion // update cycle

#region UI
        // TODO: UI button to refresh in editor
#endregion // UI


#region implementation
        void Refresh()
        {
            List<GameObject> rootObjects = new List<GameObject>();
            Scene            scene       = SceneManager.GetActiveScene();
            scene.GetRootGameObjects(rootObjects);

            foreach (var obj in rootObjects)
            {
            SkinnedMeshRenderer[] skinnedMeshRenderers = (
                from smr in obj.GetComponentsInChildren<SkinnedMeshRenderer>()
                where (
                    (from mat in smr.materials
                    where TargetedShaders.Contains(mat.shader)
                    select true).Count() > 0
                    ||
                    (from mat in smr.sharedMaterials
                    where TargetedShaders.Contains(mat.shader)
                    select true).Count() > 0)
                select smr
            ).Distinct().ToArray();


            foreach (var smr in skinnedMeshRenderers)
            {
                GameObject smrParent = smr.gameObject;
                foreach (MonoScript componentType in _componentTypes)
                {
                    Type subcontroller      = componentType.GetClass();
                    var  existingController = smrParent.GetComponent(subcontroller);
                    if (existingController == null)
                    {
                        smrParent.AddComponent(subcontroller);
                    }
                }
            }
            }
        }
#endregion // implementation
    }
} // namespace KageKirin.SpheroidNormal
