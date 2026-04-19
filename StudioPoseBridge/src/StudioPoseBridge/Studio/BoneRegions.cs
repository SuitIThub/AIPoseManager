using System.Collections.Generic;

namespace StudioPoseBridge.Game
{
    /// <summary>Region name → bone GameObject names (HS2 / AI girl naming; verify with /v1/characters/{id}/bones/all).</summary>
    public static class BoneRegions
    {
        public static readonly IReadOnlyDictionary<string, string[]> Map = new Dictionary<string, string[]>
        {
            ["head"] = new[] { "cf_j_head" },
            ["neck"] = new[] { "cf_j_neck" },
            ["torso"] = new[] { "cf_j_spine01", "cf_j_spine02", "cf_j_spine03" },
            ["hips"] = new[] { "cf_j_hips", "cf_j_waist01", "cf_j_waist02" },
            ["left_arm"] = new[] { "cf_j_shoulder_L", "cf_j_arm00_L", "cf_j_forearm01_L" },
            ["right_arm"] = new[] { "cf_j_shoulder_R", "cf_j_arm00_R", "cf_j_forearm01_R" },
            ["left_hand"] = new[]
            {
                "cf_j_hand_L",
                "cf_j_thumb01_L", "cf_j_thumb02_L", "cf_j_thumb03_L",
                "cf_j_index01_L", "cf_j_index02_L", "cf_j_index03_L",
                "cf_j_middle01_L", "cf_j_middle02_L", "cf_j_middle03_L",
                "cf_j_ring01_L", "cf_j_ring02_L", "cf_j_ring03_L",
                "cf_j_little01_L", "cf_j_little02_L", "cf_j_little03_L"
            },
            ["right_hand"] = new[]
            {
                "cf_j_hand_R",
                "cf_j_thumb01_R", "cf_j_thumb02_R", "cf_j_thumb03_R",
                "cf_j_index01_R", "cf_j_index02_R", "cf_j_index03_R",
                "cf_j_middle01_R", "cf_j_middle02_R", "cf_j_middle03_R",
                "cf_j_ring01_R", "cf_j_ring02_R", "cf_j_ring03_R",
                "cf_j_little01_R", "cf_j_little02_R", "cf_j_little03_R"
            },
            ["left_leg"] = new[] { "cf_j_thigh00_L", "cf_j_leg01_L", "cf_j_leg03_L" },
            ["right_leg"] = new[] { "cf_j_thigh00_R", "cf_j_leg01_R", "cf_j_leg03_R" },
            ["left_foot"] = new[] { "cf_j_foot_L", "cf_j_toes_L" },
            ["right_foot"] = new[] { "cf_j_foot_R", "cf_j_toes_R" }
        };

        public static readonly string[] DefaultPoseRegions =
        {
            "torso", "left_arm", "right_arm", "left_leg", "right_leg"
        };

        public static readonly string[] AllRegionNames;

        static BoneRegions()
        {
            var k = new List<string>(Map.Keys);
            k.Sort();
            AllRegionNames = k.ToArray();
        }
    }
}
