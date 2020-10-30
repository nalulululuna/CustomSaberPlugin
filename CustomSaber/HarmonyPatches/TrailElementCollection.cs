using HarmonyLib;
using UnityEngine;

namespace CustomSaber.HarmonyPatches
{
    [HarmonyPatch(typeof(TrailElementCollection), "LenToSegment")]
    static class TrailElementCollectionTrailElementCollection
    {
		static bool Prefix(float t, ref float localF, ref int __result, TrailElementCollection __instance)
        {
			float num = __instance[__instance.capacity - 2].distance * Mathf.Clamp01(t);

			int i = 0;
			while (__instance[i].distance < num && i < __instance.capacity - 1)
			{
				i++;
			}

			if (i == 0 || i == __instance.capacity - 1)
			{
				localF = 0f;
				__result = 0;
			}
			else
			{
				TrailElement trailElement = __instance[i - 1];
				localF = (num - trailElement.distance) / (__instance[i].distance - trailElement.distance);
				__result = i - 1;
			}

			return false;
		}
	}
}
