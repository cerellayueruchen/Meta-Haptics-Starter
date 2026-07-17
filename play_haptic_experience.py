#!/usr/bin/env python3
"""Play the complete VR Doctor Fish haptic sequence over VibraForge BLE.

Expected project layout (either is accepted):

    project/
      play_haptic_experience.py
      haptics/
        welcome_experience.json
        small_fish_nibble.json
        big_fish_bite.json
        jellyfish_sting.json
        idle_water.json

or place the JSON files in ``haptics/patterns/``.

Pattern files may be either:
- newline-delimited JSON (one command object per line), or
- a normal JSON array of command objects.
"""

from __future__ import annotations

import asyncio
import json
import math
from collections import defaultdict
from pathlib import Path
from typing import Any

from bleak import BleakClient, BleakScanner


CHARACTERISTIC_UUID = "f22535de-5375-44bd-8ca9-d0ea9ff9e410"
CONTROL_UNIT_NAME = "QT Py ESP32-S3"

UNITS_PER_CHAIN = 16
MAX_MOTOR_ADDR = 63
COMMANDS_PER_BLE_FRAME = 20

# Files run once, in this order. Change this list to change the experience.
PATTERN_SEQUENCE = (
    "welcome_experience",
    "small_fish_nibble",
    "big_fish_bite",
    "jellyfish_sting",
    "idle_water",
)

# Brief silence between experiences. Set to 0.0 for immediate transitions.
GAP_BETWEEN_PATTERNS_SECONDS = 0.35

# The final idle-water score currently runs once. Increase this to repeat it.
IDLE_WATER_REPETITIONS = 1

SCRIPT_DIR = Path(__file__).resolve().parent
HAPTICS_DIR = SCRIPT_DIR / "haptics"


class PatternError(ValueError):
    """Raised when a haptic pattern file is missing or malformed."""


def create_command(addr: int, mode: int, duty: int, freq: int) -> bytearray:
    """Encode one VibraForge motor command into its three-byte format."""
    serial_group = addr // UNITS_PER_CHAIN
    serial_addr = addr % UNITS_PER_CHAIN
    byte1 = (serial_group << 2) | (mode & 0x01)
    byte2 = 0x40 | (serial_addr & 0x3F)
    byte3 = 0x80 | ((duty & 0x0F) << 3) | (freq & 0x07)
    return bytearray((byte1, byte2, byte3))


def create_command_frame(commands: list[dict[str, Any]]) -> bytearray:
    """Build one fixed-length BLE frame from at most 20 commands."""
    if len(commands) > COMMANDS_PER_BLE_FRAME:
        raise ValueError(
            f"Cannot send more than {COMMANDS_PER_BLE_FRAME} commands "
            "in one BLE frame"
        )

    frame = bytearray()
    for command in commands:
        frame.extend(
            create_command(
                command["addr"],
                command["mode"],
                command["duty"],
                command["freq"],
            )
        )

    frame.extend(
        bytearray((0xFF, 0xFF, 0xFF))
        * (COMMANDS_PER_BLE_FRAME - len(commands))
    )
    return frame


def find_pattern_file(pattern_name: str) -> Path:
    """Find a score in haptics/ or haptics/patterns/."""
    candidates = (
        HAPTICS_DIR / f"{pattern_name}.json",
        HAPTICS_DIR / "patterns" / f"{pattern_name}.json",
    )
    for candidate in candidates:
        if candidate.is_file():
            return candidate

    searched = "\n  - ".join(str(path) for path in candidates)
    raise FileNotFoundError(
        f"Could not find {pattern_name}.json. Searched:\n  - {searched}"
    )


def _parse_pattern_text(path: Path, text: str) -> list[dict[str, Any]]:
    """Parse either a JSON array or newline-delimited JSON objects."""
    stripped = text.strip()
    if not stripped:
        raise PatternError(f"{path} is empty")

    if stripped.startswith("["):
        parsed = json.loads(stripped)
        if not isinstance(parsed, list):
            raise PatternError(f"{path} must contain a JSON array")
        return parsed

    commands: list[dict[str, Any]] = []
    for line_number, line in enumerate(text.splitlines(), start=1):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        try:
            command = json.loads(line)
        except json.JSONDecodeError as exc:
            raise PatternError(
                f"Invalid JSON in {path}, line {line_number}: {exc.msg}"
            ) from exc
        commands.append(command)
    return commands


def load_pattern(path: Path) -> list[dict[str, Any]]:
    """Load, validate and sort a haptic command score."""
    try:
        raw_commands = _parse_pattern_text(path, path.read_text(encoding="utf-8"))
    except OSError as exc:
        raise PatternError(f"Could not read {path}: {exc}") from exc
    except json.JSONDecodeError as exc:
        raise PatternError(f"Invalid JSON in {path}: {exc.msg}") from exc

    if not raw_commands:
        raise PatternError(f"{path} contains no commands")

    commands: list[dict[str, Any]] = []
    required_fields = ("time", "addr", "mode", "duty", "freq")

    for index, raw in enumerate(raw_commands, start=1):
        if not isinstance(raw, dict):
            raise PatternError(f"{path}: command {index} is not an object")

        missing = [field for field in required_fields if field not in raw]
        if missing:
            raise PatternError(
                f"{path}: command {index} is missing: {', '.join(missing)}"
            )

        try:
            command = {
                "time": float(raw["time"]),
                "addr": int(raw["addr"]),
                "mode": int(raw["mode"]),
                "duty": int(raw["duty"]),
                "freq": int(raw["freq"]),
            }
        except (TypeError, ValueError) as exc:
            raise PatternError(
                f"{path}: command {index} contains a non-numeric value"
            ) from exc

        if not math.isfinite(command["time"]) or command["time"] < 0:
            raise PatternError(f"{path}: command {index} has invalid time")
        if not 0 <= command["addr"] <= MAX_MOTOR_ADDR:
            raise PatternError(
                f"{path}: command {index} address must be 0-{MAX_MOTOR_ADDR}"
            )
        if command["mode"] not in (0, 1):
            raise PatternError(f"{path}: command {index} mode must be 0 or 1")
        if not 0 <= command["duty"] <= 15:
            raise PatternError(f"{path}: command {index} duty must be 0-15")
        if not 0 <= command["freq"] <= 7:
            raise PatternError(f"{path}: command {index} freq must be 0-7")

        if command["mode"] == 0:
            command["duty"] = 0
            command["freq"] = 0

        commands.append(command)

    # Stop commands are sent before start/update commands when times tie.
    commands.sort(key=lambda command: (command["time"], command["mode"]))
    return commands


async def write_commands(
    client: BleakClient, commands: list[dict[str, Any]]
) -> None:
    """Write any number of commands, splitting them into valid BLE frames."""
    for start in range(0, len(commands), COMMANDS_PER_BLE_FRAME):
        chunk = commands[start : start + COMMANDS_PER_BLE_FRAME]
        await client.write_gatt_char(
            CHARACTERISTIC_UUID,
            create_command_frame(chunk),
            response=False,
        )


async def stop_addresses(client: BleakClient, addresses: set[int]) -> None:
    """Stop the supplied motor addresses."""
    if not addresses:
        return
    commands = [
        {"addr": addr, "mode": 0, "duty": 0, "freq": 0}
        for addr in sorted(addresses)
    ]
    await write_commands(client, commands)


async def play_pattern(
    client: BleakClient,
    pattern_name: str,
    active_addresses: set[int],
) -> float:
    """Play one pattern with monotonic-clock scheduling."""
    path = find_pattern_file(pattern_name)
    commands = load_pattern(path)

    grouped: dict[float, list[dict[str, Any]]] = defaultdict(list)
    for command in commands:
        grouped[command["time"]].append(command)

    duration = max(grouped)
    print(
        f"Playing {pattern_name} "
        f"({len(commands)} commands, {duration:.3f} s)"
    )

    loop = asyncio.get_running_loop()
    started_at = loop.time()

    for timestamp in sorted(grouped):
        wait_seconds = started_at + timestamp - loop.time()
        if wait_seconds > 0:
            await asyncio.sleep(wait_seconds)

        timestamp_commands = grouped[timestamp]
        await write_commands(client, timestamp_commands)

        for command in timestamp_commands:
            if command["mode"] == 1:
                active_addresses.add(command["addr"])
            else:
                active_addresses.discard(command["addr"])

    print(f"Finished {pattern_name}")
    return duration


async def find_control_unit():
    """Scan until the named VibraForge control unit is found."""
    print(f"Scanning for BLE device: {CONTROL_UNIT_NAME!r}")
    device = await BleakScanner.find_device_by_name(
        CONTROL_UNIT_NAME,
        timeout=12.0,
    )
    if device is None:
        raise RuntimeError(
            f"Could not find BLE device {CONTROL_UNIT_NAME!r}. "
            "Check that it is powered, nearby and not connected elsewhere."
        )
    return device


async def run_experience() -> None:
    """Connect once and play the complete experience sequence."""
    # Load everything before connecting, so malformed/missing files fail safely.
    for name in PATTERN_SEQUENCE:
        path = find_pattern_file(name)
        load_pattern(path)
        print(f"Loaded {path.relative_to(SCRIPT_DIR)}")

    device = await find_control_unit()
    active_addresses: set[int] = set()

    async with BleakClient(device) as client:
        if not client.is_connected:
            raise RuntimeError("BLE connection failed")

        print(f"Connected to {device.name} at {device.address}")

        # Clear any vibration left over from a previous interrupted run.
        await stop_addresses(client, set(range(MAX_MOTOR_ADDR + 1)))

        try:
            for pattern_name in PATTERN_SEQUENCE:
                repetitions = (
                    IDLE_WATER_REPETITIONS
                    if pattern_name == "idle_water"
                    else 1
                )
                for repetition in range(repetitions):
                    if repetitions > 1:
                        print(
                            f"Idle-water loop {repetition + 1}/{repetitions}"
                        )
                    await play_pattern(client, pattern_name, active_addresses)

                # Defence in depth: a bad score cannot leak vibration into the
                # next scene, even though generated files already stop motors.
                await stop_addresses(client, active_addresses)
                active_addresses.clear()

                if pattern_name != PATTERN_SEQUENCE[-1]:
                    await asyncio.sleep(GAP_BETWEEN_PATTERNS_SECONDS)
        finally:
            print("Stopping all motors...")
            await stop_addresses(client, set(range(MAX_MOTOR_ADDR + 1)))

    print("Haptic experience complete.")


def main() -> None:
    try:
        asyncio.run(run_experience())
    except KeyboardInterrupt:
        print("\nPlayback interrupted.")
    except (FileNotFoundError, PatternError, RuntimeError) as exc:
        raise SystemExit(f"Error: {exc}") from exc


if __name__ == "__main__":
    main()