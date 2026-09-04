using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.Core
{
    public static class MagicaClothActivation // Magica 에셋 패치 없이 종료·재활성 NRE 차단
    {
        private const string MagicaAssembly = "MagicaCloth2";
        private static Type magicaClothType;
        private static Type magicaColliderType;
        private static MethodInfo isPlayingMethod;
        private static PropertyInfo colliderProperty;
        private static bool typesResolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            magicaClothType = null;
            magicaColliderType = null;
            isPlayingMethod = null;
            colliderProperty = null;
            typesResolved = false;
        }

        public static void SetActive(GameObject root, bool active)
        {
            if (root == null)
            {
                return;
            }

            if (active && !CanEnableCloth)
            {
                DisableUnder(root);
            }

            root.SetActive(active);
        }

        public static void DisableAllIfManagerAlive()
        {
            if (!CanEnableCloth)
            {
                return;
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    DisableUnder(roots[rootIndex]);
                }
            }
        }

        private static bool CanEnableCloth
        {
            get
            {
                ResolveTypes();
                if (isPlayingMethod == null || colliderProperty == null)
                {
                    return true;
                }

                return (bool)isPlayingMethod.Invoke(null, null) && colliderProperty.GetValue(null) != null;
            }
        }

        private static void DisableUnder(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            ResolveTypes();
            DisableBehaviours(root, magicaClothType);
            DisableBehaviours(root, magicaColliderType);
        }

        private static void DisableBehaviours(GameObject root, Type type)
        {
            if (type == null)
            {
                return;
            }

            Component[] components = root.GetComponentsInChildren(type, true);
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void ResolveTypes()
        {
            if (typesResolved)
            {
                return;
            }

            typesResolved = true;
            Type managerType = FindType("MagicaCloth2.MagicaManager");
            magicaClothType = FindType("MagicaCloth2.MagicaCloth");
            magicaColliderType = FindType("MagicaCloth2.ColliderComponent");
            isPlayingMethod = managerType != null
                ? managerType.GetMethod("IsPlaying", BindingFlags.Public | BindingFlags.Static)
                : null;
            colliderProperty = managerType != null
                ? managerType.GetProperty("Collider", BindingFlags.Public | BindingFlags.Static)
                : null;
        }

        private static Type FindType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, {MagicaAssembly}");
            if (type != null)
            {
                return type;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                type = assemblies[index].GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
