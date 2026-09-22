import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import baseline_publish_policy as policy


REPOSITORY = "mitchdenny/hex1b"
BEFORE = "a" * 40
AFTER = "b" * 40


def pull_request(target="main"):
    return {
        "user": {"login": "github-actions[bot]"},
        "head": {
            "ref": f"bot/baseline-bump/{target.replace('/', '-')}-to-1.2.3",
            "sha": AFTER,
            "repo": {"full_name": REPOSITORY},
        },
        "base": {
            "ref": target,
            "sha": BEFORE,
            "repo": {"full_name": REPOSITORY},
        },
        "merged_at": "2026-09-22T00:00:00Z",
        "merge_commit_sha": AFTER,
    }


def push_event(target="main"):
    return {"before": BEFORE, "after": AFTER, "ref": f"refs/heads/{target}"}


def project(version="1.2.2"):
    return (
        b'<Project>\n<PropertyGroup>\n'
        + policy.BEGIN + b"\n"
        + (f"<PackageValidationBaselineVersion>{version}"
           "</PackageValidationBaselineVersion>\n").encode()
        + policy.END + b"\n</PropertyGroup>\n</Project>\n"
    )


class IdentityTests(unittest.TestCase):
    def test_bot_branches_match_their_destination(self):
        for target in ("main", "release/1.2"):
            with self.subTest(target=target):
                pr = pull_request(target)
                self.assertTrue(policy.is_automated_baseline_pr(pr, REPOSITORY, target))
                pr["head"]["ref"] = f"bot/baseline-bump/{target.replace('/', '-')}"
                self.assertTrue(policy.is_automated_baseline_pr(pr, REPOSITORY, target))

    def test_manual_fork_and_unrelated_prs_do_not_match(self):
        cases = [
            ("user", "login", "maintainer"),
            ("head", "ref", "feature/something"),
            ("head", "ref", "bot/baseline-bump/main-malicious"),
            ("head", "ref", "bot/baseline-bump/release-1.2-to-1.2.3"),
            ("head", "repo", {"full_name": "someone/hex1b"}),
            ("head", "repo", None),
            ("base", "repo", {"full_name": "someone/hex1b"}),
            ("base", "ref", "release/1.2"),
        ]
        for section, key, value in cases:
            with self.subTest(section=section, key=key, value=value):
                pr = pull_request()
                pr[section][key] = value
                self.assertFalse(policy.is_automated_baseline_pr(pr, REPOSITORY, "main"))
        self.assertFalse(policy.is_automated_baseline_pr(
            pull_request("feature/test"), REPOSITORY, "feature/test",
        ))


class EventTests(unittest.TestCase):
    def test_manual_dispatch_always_publishes_without_git_or_api(self):
        with patch.object(policy, "command") as command:
            for release in (True, False):
                self.assertFalse(policy.skip_publish(
                    "workflow_dispatch", {"inputs": {"release": release}}, REPOSITORY,
                ))
            command.assert_not_called()

    def test_pr_uses_merge_base_and_actual_head(self):
        with (
            patch.object(policy, "command", return_value=(BEFORE + "\n").encode()) as command,
            patch.object(policy, "baseline_only_change", return_value=True) as diff,
        ):
            self.assertTrue(policy.skip_publish(
                "pull_request", {"pull_request": pull_request()}, REPOSITORY,
            ))
            command.assert_called_once_with("git", "merge-base", BEFORE, AFTER)
            diff.assert_called_once_with(BEFORE, AFTER)

    def test_non_bot_pr_never_checks_git(self):
        pr = pull_request()
        pr["user"]["login"] = "maintainer"
        with patch.object(policy, "command") as command:
            self.assertFalse(policy.skip_publish(
                "pull_request", {"pull_request": pr}, REPOSITORY,
            ))
            command.assert_not_called()

    def test_pr_with_other_changes_publishes(self):
        with (
            patch.object(policy, "command", return_value=BEFORE.encode()),
            patch.object(policy, "baseline_only_change", return_value=False),
        ):
            self.assertFalse(policy.skip_publish(
                "pull_request", {"pull_request": pull_request()}, REPOSITORY,
            ))

    def test_merged_push_uses_paginated_identity_not_titles(self):
        for target in ("main", "release/1.2"):
            for title in ("Edited squash title", "Merge pull request #123"):
                with self.subTest(target=target, title=title):
                    pr = pull_request(target)
                    pr["title"] = title
                    with (
                        patch.object(policy, "baseline_only_change", return_value=True) as diff,
                        patch.object(policy, "command", return_value=json.dumps([[], [pr]]).encode()) as command,
                    ):
                        self.assertTrue(policy.skip_publish("push", push_event(target), REPOSITORY))
                        diff.assert_called_once_with(BEFORE, AFTER)
                        command.assert_called_once_with(
                            "gh", "api", "--paginate", "--slurp",
                            f"repos/{REPOSITORY}/commits/{AFTER}/pulls",
                        )

    def test_push_requires_bot_pr_merged_at_this_commit_into_this_branch(self):
        prs = [[], [pull_request()]]
        prs[1][0]["user"]["login"] = "maintainer"
        for field, value in (
            ("merged_at", None), ("merge_commit_sha", BEFORE),
            ("base", pull_request("release/1.2")["base"]),
        ):
            pr = pull_request()
            pr[field] = value
            prs.append([pr])
        for matches in prs:
            with self.subTest(matches=matches), (
                patch.object(policy, "baseline_only_change", return_value=True)
            ), patch.object(policy, "command", return_value=json.dumps([matches]).encode()):
                self.assertFalse(policy.skip_publish("push", push_event(), REPOSITORY))

    def test_new_deleted_forced_and_zero_sha_pushes_are_not_exempt(self):
        for field, value in (
            ("created", True), ("deleted", True), ("forced", True),
            ("before", "0" * 40), ("after", "0" * 40), ("ref", "refs/tags/v1.2.3"),
        ):
            event = push_event()
            event[field] = value
            with self.subTest(field=field), patch.object(policy, "command") as command:
                self.assertFalse(policy.skip_publish("push", event, REPOSITORY))
                command.assert_not_called()

    def test_mixed_push_never_looks_up_prs(self):
        with (
            patch.object(policy, "baseline_only_change", return_value=False),
            patch.object(policy, "command") as command,
        ):
            self.assertFalse(policy.skip_publish("push", push_event(), REPOSITORY))
            command.assert_not_called()

    def test_api_and_git_errors_propagate(self):
        error = subprocess.CalledProcessError(1, ["gh", "api"])
        with (
            patch.object(policy, "baseline_only_change", return_value=True),
            patch.object(policy, "command", side_effect=error),
        ):
            with self.assertRaises(subprocess.CalledProcessError):
                policy.skip_publish("push", push_event(), REPOSITORY)
            with self.assertRaises(subprocess.CalledProcessError):
                policy.skip_publish("pull_request", {"pull_request": pull_request()}, REPOSITORY)

    def test_malformed_api_response_is_an_error(self):
        with (
            patch.object(policy, "baseline_only_change", return_value=True),
            patch.object(policy, "command", return_value=b"not JSON"),
        ):
            with self.assertRaises(json.JSONDecodeError):
                policy.skip_publish("push", push_event(), REPOSITORY)

    def test_invalid_sha_is_rejected(self):
        event = push_event()
        event["before"] = "--help"
        with self.assertRaises(ValueError):
            policy.skip_publish("push", event, REPOSITORY)

    def test_main_writes_explicit_output_only_on_success(self):
        with tempfile.TemporaryDirectory() as directory:
            event_path = Path(directory) / "event.json"
            output_path = Path(directory) / "output"
            event_path.write_text("{}")
            env = {
                "GITHUB_EVENT_PATH": str(event_path),
                "GITHUB_OUTPUT": str(output_path),
                "GITHUB_EVENT_NAME": "workflow_dispatch",
                "GITHUB_REPOSITORY": REPOSITORY,
            }
            with patch.dict(os.environ, env), patch("builtins.print"):
                for result in (True, False):
                    with patch.object(policy, "skip_publish", return_value=result):
                        policy.main()
                expected = "skip_publish=true\nskip_publish=false\n"
                self.assertEqual(expected, output_path.read_text())
                with patch.object(policy, "skip_publish", side_effect=ValueError("bad event")):
                    with self.assertRaises(ValueError):
                        policy.main()
                self.assertEqual(expected, output_path.read_text())


class GitDiffTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.path = self.root / policy.PROJECT
        self.path.parent.mkdir(parents=True)
        self.git("init", "-q")
        self.git("config", "user.name", "Policy Test")
        self.git("config", "user.email", "policy-test@example.invalid")
        self.git("config", "commit.gpgsign", "false")
        self.path.write_bytes(project())
        self.before = self.commit()

    def git(self, *args):
        return subprocess.run(
            ["git", "-C", str(self.root), *args], check=True, stdout=subprocess.PIPE,
        ).stdout

    def commit(self):
        self.git("add", ".")
        self.git("commit", "-qm", "fixture")
        return self.git("rev-parse", "HEAD").decode().strip()

    def classify(self, after):
        original = policy.command

        def run(*args):
            return original("git", "-C", str(self.root), *args[1:])

        with patch.object(policy, "command", side_effect=run):
            return policy.baseline_only_change(self.before, after)

    def test_baseline_change_is_exempt(self):
        self.path.write_bytes(project("1.2.3"))
        self.assertTrue(self.classify(self.commit()))

    def test_empty_baseline_block_can_be_initialized(self):
        self.path.write_bytes(project().replace(
            b"<PackageValidationBaselineVersion>1.2.2</PackageValidationBaselineVersion>", b"",
        ))
        self.before = self.commit()
        self.path.write_bytes(project("1.2.3"))
        self.assertTrue(self.classify(self.commit()))

    def test_no_change_is_not_exempt(self):
        self.assertFalse(self.classify(self.before))

    def test_missing_markers_in_previous_revision_are_not_exempt(self):
        self.path.write_bytes(project().replace(policy.BEGIN, b""))
        self.before = self.commit()
        self.path.write_bytes(project("1.2.3"))
        self.assertFalse(self.classify(self.commit()))

    def test_extra_file_in_earlier_push_commit_is_not_exempt(self):
        (self.root / "code.cs").write_text("changed")
        self.commit()
        self.path.write_bytes(project("1.2.3"))
        self.assertFalse(self.classify(self.commit()))

    def test_executable_mode_change_is_not_exempt(self):
        self.path.write_bytes(project("1.2.3"))
        self.git("add", ".")
        self.git("update-index", "--chmod=+x", policy.PROJECT)
        self.git("commit", "-qm", "mode")
        self.assertFalse(self.classify(self.git("rev-parse", "HEAD").decode().strip()))

    def test_non_baseline_edits_and_malformed_blocks_are_not_exempt(self):
        cases = [
            project("1.2.3").replace(b"<PropertyGroup>", b"<PropertyGroup Other='true'>"),
            project("1.2.3") + b"<!-- outside -->",
            project("1.2.3").replace(policy.BEGIN, b""),
            project("1.2.3").replace(policy.END, b""),
            project("1.2.3").replace(policy.END, policy.BEGIN),
            project("1.2.3").replace(policy.BEGIN, b"MARKER")
            .replace(policy.END, policy.BEGIN).replace(b"MARKER", policy.END),
            project("1.2.3") + policy.BEGIN,
            project("1.2.3").replace(b"</PackageValidationBaselineVersion>",
                                    b"</PackageValidationBaselineVersion><Other>true</Other>"),
            project("1.2.3-preview"),
        ]
        for content in cases:
            with self.subTest(content=content):
                self.path.write_bytes(content)
                self.assertFalse(self.classify(self.commit()))


if __name__ == "__main__":
    unittest.main()
