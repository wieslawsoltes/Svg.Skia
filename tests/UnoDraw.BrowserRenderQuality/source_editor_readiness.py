"""Observe source-dialog completion without issuing or retrying an editor command."""
from __future__ import annotations

import re
from typing import Any


_COMPLETED = re.compile(r"\[EditorInput ([1-9][0-9]*) ([0-9]+)\] modal-completed")
_INPUT_EVENT = re.compile(r"\[EditorInput ([1-9][0-9]*) ([0-9]+)\] .+")
_PROOF_FIELDS = {"console_start", "console_end", "console_index", "sequence", "timestamp_ms"}


def source_capture_timeout_ms(dpr: float) -> int:
    """Bound diagnostic source-dialog captures at the three native test scales.

    This does not change the scene stability deadline, comparison tolerances or
    any primary workload. A slow capture remains a failure after this budget.
    """
    if isinstance(dpr, bool) or dpr not in (1, 1.25, 2):
        raise ValueError("Source capture requires a fixed quality-matrix DPR")
    return round(30_000 * dpr * dpr)


def completed_modal(console: list[dict[str, Any]], start: int) -> dict[str, int] | None:
    """Return only a fresh, unambiguous real modal-completion observation."""
    if type(start) is not int or not 0 <= start <= len(console):
        raise ValueError("The console checkpoint must belong to this observation range")
    completed = []
    for index in range(start, len(console)):
        text = console[index].get("text")
        match = _COMPLETED.fullmatch(text.strip()) if isinstance(text, str) else None
        if match:
            if console[index].get("type") not in {"log", "info", "debug"}:
                raise AssertionError("Modal completion must be an informational runtime console entry")
            completed.append({"console_index": index, "sequence": int(match[1]),
                              "timestamp_ms": int(match[2])})
    if len(completed) > 1:
        raise AssertionError("More than one modal completed after the source action checkpoint")
    return completed[0] if completed else None


def verify_source_editor_readiness(case: dict[str, Any], *, required: bool) -> None:
    """Reconstruct both source-modal observations from their retained navigation.

    The source-bound driver records each begin checkpoint immediately before its
    action and the exclusive end checkpoint when real modal completion arrives.
    This validates those original observations; it does not issue an editor
    command, extend a deadline, infer pixels or require an already-passing case.
    Historical reports without any new readiness fields retain their old path.
    """
    if type(required) is not bool:
        raise ValueError("Source-modal readiness requires an explicit report policy")
    source_edit = case.get("source_edit")
    has_proof = isinstance(source_edit, dict) and any(
        name in source_edit for name in ("apply_completion", "close_completion"))
    if not required:
        if ("input_diagnostics" in case or "source_editor_capture_timeout_ms" in case
                or "source_edit_progress" in case or has_proof):
            raise ValueError("Case source-modal readiness fields require the matching report policy")
        return
    if case.get("input_diagnostics") is not True or not isinstance(source_edit, dict):
        raise ValueError("The source-modal report requires enabled case diagnostics and both observations")
    dpr = case.get("dpr")
    timeout = case.get("source_editor_capture_timeout_ms")
    if type(timeout) is not int or timeout != source_capture_timeout_ms(dpr):
        raise ValueError("The source-editor capture budget does not match its fixed native DPR")
    environment = case.get("environment")
    if (not isinstance(environment, dict) or isinstance(environment.get("device_pixel_ratio"), bool)
            or environment.get("device_pixel_ratio") != dpr):
        raise ValueError("The source-modal observation and actual environment must use the same native DPR")
    console = case.get("console")
    if not isinstance(console, list) or not all(isinstance(entry, dict) for entry in console):
        raise ValueError("Source-modal readiness requires the original navigation console")

    observations = []
    for action in ("apply_completion", "close_completion"):
        proof = source_edit.get(action)
        if (not isinstance(proof, dict) or set(proof) != _PROOF_FIELDS
                or any(type(value) is not int for value in proof.values())):
            raise ValueError("Each source-modal proof requires its exact integer boundary and event metadata")
        start, end, index = proof["console_start"], proof["console_end"], proof["console_index"]
        if (not 0 <= start <= index < end <= len(console)
                or proof["sequence"] <= 0 or proof["timestamp_ms"] < 0):
            raise ValueError("Source-modal completion must fall inside its own bounded navigation slice")
        observed = completed_modal(console[:end], start)
        if observed != {key: proof[key] for key in ("console_index", "sequence", "timestamp_ms")}:
            raise ValueError("The source-modal proof differs from its actual bounded console observation")
        # EditorInputTrace assigns a unique sequence to every runtime entry.
        # A copied observation cannot borrow another action's original sequence.
        same_sequence = []
        for entry_index, entry in enumerate(console):
            text = entry.get("text")
            match = _INPUT_EVENT.fullmatch(text.strip()) if isinstance(text, str) else None
            if match and int(match[1]) == proof["sequence"]:
                same_sequence.append(entry_index)
        if same_sequence != [index]:
            raise ValueError("A source-modal event sequence was duplicated or replayed in this navigation")
        observations.append(proof)

    applied, closed = observations
    if (applied["console_end"] > closed["console_start"]
            or applied["sequence"] >= closed["sequence"]
            or applied["timestamp_ms"] > closed["timestamp_ms"]):
        raise ValueError("Fresh Apply completion must precede the separate readback-close observation")
    if completed_modal(console, closed["console_end"]) is not None:
        raise ValueError("A late modal completion follows the recorded source-close boundary")
