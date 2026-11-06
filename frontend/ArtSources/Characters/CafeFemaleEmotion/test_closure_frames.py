import unittest

from closure_frames import closure_frames


class ClosureFramesTests(unittest.TestCase):
    def test_reimport_keeps_existing_closed_frame_once(self):
        frames = [dict(weight=w, vertices=[w]) for w in (25, 50, 75, 90, 100)]
        self.assertEqual(closure_frames(frames), frames)

    def test_base_lids_get_a_closed_hold_without_changing_input(self):
        frames = [dict(weight=w, vertices=[w]) for w in (25, 50, 75, 100)]
        result = closure_frames(frames)
        self.assertEqual([f["weight"] for f in result], [25, 50, 75, 90, 100])
        self.assertEqual(result[-2]["vertices"], frames[-1]["vertices"])
        self.assertEqual(result[-1], frames[-1])
        self.assertEqual([f["weight"] for f in frames], [25, 50, 75, 100])

    def test_stationary_part_has_no_closure_frames(self):
        self.assertEqual(closure_frames([]), [])


if __name__ == "__main__":
    unittest.main()
