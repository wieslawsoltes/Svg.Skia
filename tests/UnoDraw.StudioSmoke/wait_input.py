#!/usr/bin/env python3
"""Wait for a new native input observation, without replaying an earlier event."""
from __future__ import annotations

import argparse
from pathlib import Path
import re
import time


def wait_for_input(path: Path, offset: int, pattern: str, timeout: float = 10) -> None:
    if offset < 0 or timeout <= 0:
        raise ValueError("A nonnegative checkpoint and positive timeout are required.")
    expected = re.compile(pattern)
    deadline = time.monotonic() + timeout
    pending = b""
    diagnostic = ""
    with path.open("rb") as stream:
        stream.seek(offset)
        while time.monotonic() < deadline:
            chunk = stream.read(65536)
            if chunk:
                pending += chunk
                lines = pending.split(b"\n")
                pending = lines.pop()
                for line in lines:
                    decoded = line.decode("utf-8", errors="replace")
                    diagnostic = (diagnostic + decoded + "\n")[-8192:]
                    if expected.search(decoded):
                        return
            else:
                time.sleep(0.02)
    raise TimeoutError(f"Native input did not reach {pattern!r} after checkpoint {offset}.\n{diagnostic}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("log", type=Path)
    parser.add_argument("offset", type=int)
    parser.add_argument("pattern")
    parser.add_argument("--timeout", type=float, default=10)
    args = parser.parse_args()
    wait_for_input(args.log, args.offset, args.pattern, args.timeout)
