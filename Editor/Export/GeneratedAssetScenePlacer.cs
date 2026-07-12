using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Places or reselects a generated prefab in the current scene, wired to undo.
    public static class GeneratedAssetScenePlacer
    {
        public sealed class PlaceResult
        {
            public bool Success;
            public string Message;
            public GameObject Instance;
        }

        // Returns true when the generated prefab already has a linked root instance in the active scene.
        public static bool HasSceneInstance(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return false;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab != null && FindExistingInstance(prefab) != null;
        }

        // Load the prefab at prefabPath, reuse an existing scene instance when possible, otherwise place it.
        // Returns failure when the path is missing, the asset cannot be loaded, or instantiation fails.
        public static PlaceResult PlacePrefab(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
                return new PlaceResult { Success = false, Message = "No generated prefab path is available.", Instance = null };

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return new PlaceResult { Success = false, Message = "Generated prefab could not be loaded: " + prefabPath, Instance = null };

            var existing = FindExistingInstance(prefab);
            if (existing != null)
            {
                FocusInstance(existing);
                return new PlaceResult
                {
                    Success = true,
                    Message = "Selected existing generated prefab in the current scene: " + prefab.name,
                    Instance = existing
                };
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return new PlaceResult { Success = false, Message = "Generated prefab could not be placed in the scene.", Instance = null };

            Undo.RegisterCreatedObjectUndo(instance, "Place Generated Smear Asset");
            instance.name = prefab.name;

            FocusInstance(instance);

            return new PlaceResult
            {
                Success = true,
                Message = "Placed generated prefab in the current scene: " + prefab.name,
                Instance = instance
            };
        }

        // Finds an existing root prefab instance linked to the generated prefab asset.
        private static GameObject FindExistingInstance(GameObject prefab)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;

            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var transforms = roots[r].GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    var go = transforms[t].gameObject;
                    if (PrefabUtility.GetNearestPrefabInstanceRoot(go) != go) continue;
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(go) as GameObject;
                    if (source == prefab) return go;
                }
            }

            return null;
        }

        // Selects, pings, and frames the scene instance for immediate inspection.
        private static void FocusInstance(GameObject instance)
        {
            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }
    }
}
