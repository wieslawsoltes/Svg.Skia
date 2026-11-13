import copy
import unittest

from source_editor_readiness import completed_modal, source_capture_timeout_ms, verify_source_editor_readiness


def event(sequence=1, timestamp=42, stage="modal-completed"):
    return {"type": "log", "text": f"[EditorInput {sequence} {timestamp}] {stage}"}


def readiness_case(dpr=1):
    stages = ("modal-completed", "modal-opened", "source-pointer", "modal-closed result=Primary",
              "modal-completed", "modal-completed", "modal-opened", "source-escape",
              "modal-completed", "canvas-input")
    return {"dpr": dpr, "input_diagnostics": True,
            "source_editor_capture_timeout_ms": source_capture_timeout_ms(dpr),
            "environment": {"device_pixel_ratio": dpr},
            "console": [event(index + 1, (index + 1) * 10, stage) for index, stage in enumerate(stages)],
            "source_edit": {
                "apply_completion": {"console_start": 3, "console_end": 5, "console_index": 4,
                                     "sequence": 5, "timestamp_ms": 50},
                "close_completion": {"console_start": 7, "console_end": 9, "console_index": 8,
                                     "sequence": 9, "timestamp_ms": 90}}}


class SourceEditorReadinessTests(unittest.TestCase):
    def test_capture_budgets_are_bounded_at_the_original_native_scales(self):
        self.assertEqual([source_capture_timeout_ms(dpr) for dpr in (1, 1.25, 2)],
                         [30_000, 46_875, 120_000])

    def test_capture_rejects_unqualified_scales(self):
        for dpr in (True, False, 0, -1, 1.5, 3, float("inf"), float("nan")):
            with self.subTest(dpr=dpr), self.assertRaises(ValueError):
                source_capture_timeout_ms(dpr)

    def test_previous_palette_completion_cannot_prove_source_apply(self):
        console = [event(), event(2, stage="modal-opened")]
        self.assertIsNone(completed_modal(console, 1))
        console.append(event(3, 84))
        self.assertEqual(completed_modal(console, 1),
                         {"console_index": 2, "sequence": 3, "timestamp_ms": 84})

    def test_opened_or_closed_does_not_prove_awaited_completion(self):
        console = [event(stage=stage) for stage in
                   ("modal-opened", "modal-closed result=Primary", "modal-show-request",
                    "modal-focus-restore disposed=False modal=False")]
        self.assertIsNone(completed_modal(console, 0))

    def test_unrelated_or_malformed_console_text_cannot_prove_completion(self):
        console = [{"text": value} for value in
                   (None, 42, "modal-completed", "[EditorInput 0 42] modal-completed",
                    "[EditorInput 1 -42] modal-completed", "[EditorInput 1 42] modal-completed extra",
                    "prefix [EditorInput 1 42] modal-completed")]
        self.assertIsNone(completed_modal(console, 0))

    def test_competing_modal_completion_is_ambiguous(self):
        with self.assertRaisesRegex(AssertionError, "More than one modal"):
            completed_modal([event(), event(2)], 0)

    def test_checkpoint_must_belong_to_the_retained_navigation(self):
        for checkpoint in (-1, 2, 1.0, True, None):
            with self.subTest(checkpoint=checkpoint), self.assertRaises(ValueError):
                completed_modal([event()], checkpoint)
        self.assertIsNone(completed_modal([event()], 1))

    def test_log_newline_keeps_exact_event_identity(self):
        console = [{"type": "log", "text": "[EditorInput 7 1234] modal-completed\n"}]
        self.assertEqual(completed_modal(console, 0),
                         {"console_index": 0, "sequence": 7, "timestamp_ms": 1234})

    def test_only_informational_runtime_entries_can_prove_completion(self):
        for severity in ("log", "info", "debug"):
            self.assertIsNotNone(completed_modal([{**event(), "type": severity}], 0))
        for severity in (None, "error", "warning", "warn", ""):
            with self.subTest(severity=severity), self.assertRaises(AssertionError):
                completed_modal([{**event(), "type": severity}], 0)

    def test_current_proof_reconstructs_both_actions_without_a_passing_flag(self):
        for dpr in (1, 1.25, 2):
            case = readiness_case(dpr)
            case["passed"] = False
            original = copy.deepcopy(case)
            verify_source_editor_readiness(case, required=True)
            self.assertEqual(case, original)

    def test_historical_absence_remains_readable_but_cannot_hide_partial_new_fields(self):
        historical = {"dpr": 1, "source_edit": {"reopened_after_apply": True}}
        verify_source_editor_readiness(historical, required=False)
        for mutation in ({"input_diagnostics": False}, {"input_diagnostics": None},
                         {"source_editor_capture_timeout_ms": 30_000},
                         {"source_edit_progress": {}},
                         {"source_edit": {"apply_completion": None}},
                         {"source_edit": {"close_completion": None}}):
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                verify_source_editor_readiness({**historical, **mutation}, required=False)
        with self.assertRaises(ValueError):
            verify_source_editor_readiness(readiness_case(), required=False)
        for policy in (None, 0, 1, "true"):
            with self.subTest(policy=policy), self.assertRaises(ValueError):
                verify_source_editor_readiness(historical, required=policy)

    def test_enabled_case_flags_and_exact_dpr_budget_cannot_be_stripped_or_relabelled(self):
        for field, value in (("input_diagnostics", False), ("input_diagnostics", 1),
                             ("source_editor_capture_timeout_ms", 30_000.0),
                             ("source_editor_capture_timeout_ms", 46_875), ("dpr", 1.25)):
            case = readiness_case()
            case[field] = value
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                verify_source_editor_readiness(case, required=True)
        for field in ("input_diagnostics", "source_editor_capture_timeout_ms", "console", "source_edit"):
            case = readiness_case()
            case.pop(field)
            with self.subTest(missing=field), self.assertRaises(ValueError):
                verify_source_editor_readiness(case, required=True)
        for dpr in (None, True, 1.25, 2):
            case = readiness_case()
            case["environment"]["device_pixel_ratio"] = dpr
            with self.subTest(environment_dpr=dpr), self.assertRaises(ValueError):
                verify_source_editor_readiness(case, required=True)

    def test_both_proofs_require_exact_integer_metadata_and_actual_event_identity(self):
        for action in ("apply_completion", "close_completion"):
            case = readiness_case()
            case["source_edit"].pop(action)
            with self.subTest(action=action, missing=True), self.assertRaises(ValueError):
                verify_source_editor_readiness(case, required=True)
            for field in readiness_case()["source_edit"][action]:
                for value in (None, True, "1", 1.0, float("nan")):
                    case = readiness_case()
                    case["source_edit"][action][field] = value
                    with self.subTest(action=action, field=field, value=value), self.assertRaises(ValueError):
                        verify_source_editor_readiness(case, required=True)
            for field, value in (("console_start", -1), ("console_end", 100), ("console_index", 0),
                                 ("sequence", 999), ("timestamp_ms", 999), ("unrecorded", 1)):
                case = readiness_case()
                case["source_edit"][action][field] = value
                with self.subTest(action=action, field=field, value=value), self.assertRaises(ValueError):
                    verify_source_editor_readiness(case, required=True)

    def test_fresh_ranges_reject_early_late_ambiguous_and_severity_mismatched_events(self):
        for action in ("apply_completion", "close_completion"):
            for defect in ("before-start", "at-exclusive-end", "ambiguous", "malformed", "severity"):
                case = readiness_case()
                proof = case["source_edit"][action]
                index = proof["console_index"]
                if defect == "before-start":
                    proof["console_start"] = index + 1
                elif defect == "at-exclusive-end":
                    proof["console_end"] = index
                elif defect == "ambiguous":
                    case["console"][proof["console_start"]] = event(99, 40)
                elif defect == "malformed":
                    case["console"][index]["text"] += " extra"
                else:
                    case["console"][index]["type"] = "error"
                with self.subTest(action=action, defect=defect), self.assertRaises((AssertionError, ValueError)):
                    verify_source_editor_readiness(case, required=True)

    def test_apply_and_close_must_have_disjoint_ordered_fresh_identities(self):
        for defect in ("same-proof", "swapped", "overlap", "sequence", "timestamp", "replayed", "late"):
            case = readiness_case()
            applied, closed = case["source_edit"]["apply_completion"], case["source_edit"]["close_completion"]
            if defect == "same-proof":
                case["source_edit"]["close_completion"] = copy.deepcopy(applied)
            elif defect == "swapped":
                case["source_edit"]["apply_completion"], case["source_edit"]["close_completion"] = closed, applied
            elif defect == "overlap":
                case["console"][5] = event(6, 60, "modal-opened")
                applied["console_end"], closed["console_start"] = 6, 5
            elif defect == "sequence":
                applied["sequence"] = 99
                case["console"][applied["console_index"]] = event(99, applied["timestamp_ms"])
            elif defect == "timestamp":
                applied["timestamp_ms"] = 100
                case["console"][applied["console_index"]] = event(applied["sequence"], 100)
            elif defect == "replayed":
                case["console"][5] = event(applied["sequence"], applied["timestamp_ms"])
            else:
                case["console"].append(event(11, 110))
            with self.subTest(defect=defect), self.assertRaises((AssertionError, ValueError)):
                verify_source_editor_readiness(case, required=True)


if __name__ == "__main__":
    unittest.main()
