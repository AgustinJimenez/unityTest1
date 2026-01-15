using UnityEditor;
using UnityEngine;

public partial class ThirdPersonSetup
{
    private static void DescribeDryRunPlan()
    {
        ReportInfo("Would clean up existing Player/Ground and camera scripts.");
        ReportInfo("Would create Ground, ramp, stairs, lighting, Player, and camera setup.");
        ReportInfo("Would configure Input actions and CharacterController sizing.");

        string dummyPrefab = ThirdPersonSetupConfig.DummyPrefabPath;
        GameObject dummy = AssetDatabase.LoadAssetAtPath<GameObject>(dummyPrefab);
        if (dummy != null)
        {
            ReportInfo($"Would apply character model: {dummyPrefab}");
        }
        else
        {
            ReportWarning("Preferred character prefab not found; would fall back to Mixamo/character search.");
        }

        ReportInfo("Would configure avatar + animator controller and wire locomotion/jump/sprint states.");
        ReportInfo("Would attempt to fix materials and textures.");
    }
}
