using HarmonyLib;

namespace CustomSaber.HarmonyPatches
{
    [HarmonyPatch(typeof(SaberTrail), "Awake")]
    static class SaberTrailAwake
    {
        static bool Prefix(SaberTrailRenderer ____trailRendererPrefab)
        {            
            return ____trailRendererPrefab != null;
        }
    }

    [HarmonyPatch(typeof(SaberTrail), "LateUpdate")]
    static class SaberTrailLateUpdate
    {
        static bool Prefix(IBladeMovementData ____movementData)
        {
            return ____movementData != null;
        }
    }
}
