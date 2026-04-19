import asyncio
import sys

from studio_pose_mcp.config import settings
from studio_pose_mcp.server import run


def main() -> None:
    try:
        s = settings()
    except RuntimeError as e:
        print(str(e), file=sys.stderr)
        print(
            "Hint: start StudioNEOV2 with Studio Pose Bridge, then copy POSE_PLUGIN_TOKEN from BepInEx log.",
            file=sys.stderr,
        )
        sys.exit(1)
    asyncio.run(run(s))


if __name__ == "__main__":
    main()
