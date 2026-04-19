DEFAULT_REGIONS = ["torso", "left_arm", "right_arm", "left_leg", "right_leg"]

ALL_REGION_KEYS = [
    "head",
    "neck",
    "torso",
    "hips",
    "left_arm",
    "right_arm",
    "left_hand",
    "right_hand",
    "left_leg",
    "right_leg",
    "left_foot",
    "right_foot",
]


def parse_regions_csv(s: str) -> list[str]:
    return [p.strip() for p in s.split(",") if p.strip()]
