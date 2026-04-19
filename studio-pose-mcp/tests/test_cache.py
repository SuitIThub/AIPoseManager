from studio_pose_mcp.cache import PoseCache


def test_pose_invalidation():
    c = PoseCache()
    c.set_pose(1, {"x": 1})
    c.invalidate_char(1)
    assert 1 not in c.last_pose_by_char


def test_screenshot_dedup():
    c = PoseCache()
    b64 = "abcd"
    u1, _ = c.set_screenshot(1, "front", 512, "full_body", b64)
    assert u1 is False
    u2, prev = c.set_screenshot(1, "front", 512, "full_body", b64)
    assert u2 is True
    assert prev is not None
