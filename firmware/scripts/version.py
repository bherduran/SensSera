# PlatformIO pre-script: defines FW_VERSION, which the board sends with every upload
# and the dashboard shows. CI passes FIRMWARE_VERSION; local builds use the git commit.
import os
import subprocess

Import("env")  # noqa: F821 (provided by PlatformIO)

BASE = "0.1.0"


def git(*args):
    try:
        return subprocess.check_output(["git", *args], text=True, stderr=subprocess.DEVNULL).strip()
    except (OSError, subprocess.CalledProcessError):
        return ""


version = os.environ.get("FIRMWARE_VERSION")
if not version:
    sha = git("rev-parse", "--short", "HEAD") or "unknown"
    dirty = ".dirty" if git("status", "--porcelain", "--", ".") else ""
    version = f"{BASE}+{sha}{dirty}"

# Node.FirmwareVersion is varchar(50) on the server.
version = version[:50]
print(f"Firmware version: {version}")
env.Append(CPPDEFINES=[("FW_VERSION", env.StringifyMacro(version))])  # noqa: F821
