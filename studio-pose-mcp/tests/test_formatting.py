from studio_pose_mcp.formatting import filter_regions_dict, format_pose_compact


def test_compact_pose():
    pose = {
        "regions": {
            "torso": [
                {"bone": "cf_j_spine01", "rot_euler": [1.1, 2.2, 3.3]},
            ]
        }
    }
    s = format_pose_compact(pose, ["torso"])
    assert "cf_j_spine01: 1.1,2.2,3.3" in s


def test_filter_regions():
    d = {"a": 1, "b": 2}
    assert filter_regions_dict(d, {"a"}) == {"a": 1}
