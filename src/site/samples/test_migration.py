import json
import os
import pathlib
import unittest
import xml.etree.ElementTree as ET

import adapt
import migrate

ROOT = pathlib.Path(__file__).resolve().parent


class SampleMigrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = json.loads((ROOT / "catalog.json").read_text())
        cls.inventory = json.loads((ROOT / "inventory.json").read_text())

    def test_catalog_every_complete_demo_has_standalone_project(self):
        expected = {(r["page"], r["codeKey"]) for r in migrate.inventory()
                    if r["status"] in {"standalone", "source-only"}}
        actual = {(r["page"], r["codeKey"]) for r in self.catalog}
        self.assertEqual(expected, actual)
        self.assertEqual(len(actual), len(self.catalog))
        self.assertEqual(len({r["id"] for r in self.catalog}), len(self.catalog))

    def test_inventory_all_snippets_have_explicit_dispositions(self):
        actual = {(r["page"], r["codeKey"], r["kind"], r["status"]) for r in self.inventory}
        expected = {(r["page"], r["codeKey"], r["kind"], r["status"]) for r in migrate.inventory()}
        self.assertEqual(expected, actual)
        for record in self.inventory:
            self.assertNotEqual(record["status"], "needs-review", record)
            if record["status"] != "standalone":
                self.assertTrue(record["reason"], record)

    def test_projects_are_independent_pinned_and_licensed(self):
        license_text = (migrate.REPO / "LICENSE").read_text()
        for entry in self.catalog:
            with self.subTest(sample=entry["id"]):
                self.assertRegex(entry["id"], r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
                directory = ROOT / entry["id"]
                self.assertEqual(entry["project"], f"samples/{entry['id']}/Sample.csproj")
                project = ET.fromstring((directory / "Sample.csproj").read_text())
                self.assertEqual(project.findtext(".//TargetFramework"), "net10.0")
                self.assertEqual(project.findtext(".//OutputType"), "Exe")
                self.assertEqual(project.findtext(".//ImplicitUsings"), "enable")
                self.assertEqual(project.findtext(".//Nullable"), "enable")
                self.assertEqual(project.findtext(".//AssemblyName") or "Sample", "Sample")
                self.assertEqual(project.findall(".//ProjectReference"), [])
                packages = project.findall(".//PackageReference")
                self.assertEqual([(p.get("Include"), p.get("Version")) for p in packages], [("Hex1b", "0.166.0")])
                self.assertTrue((directory / "Program.cs").read_text().strip())
                self.assertEqual((directory / "LICENSE").read_text(), license_text)
                self.assertIn("dotnet run", (directory / "README.md").read_text())
                self.assertIn(entry["page"], (directory / "README.md").read_text())
                self.assertIn("bin/", (directory / ".gitignore").read_text())
                self.assertIn("obj/", (directory / ".gitignore").read_text())
                self.assertGreater(entry["columns"], 0)
                self.assertGreater(entry["rows"], 0)

    def test_recording_metadata_uses_safe_generic_inputs(self):
        for entry in self.catalog:
            with self.subTest(sample=entry["id"]):
                if "readyText" in entry:
                    self.assertIsInstance(entry["readyText"], str)
                    self.assertTrue(entry["readyText"].strip())
                    self.assertNotIn("\n", entry["readyText"])
                for action in entry.get("actions", []):
                    self.assertEqual(action, {"key": "Tab"}, "Focus navigation must not activate sample actions.")
                if entry["id"].startswith(("terminal-", "building-clis-")):
                    self.assertEqual(entry.get("actions"), [], "Explicitly override driver defaults for shells and flow prompts.")

    def test_finite_flow_programs_declare_natural_exit_capture(self):
        finite = set()
        for entry in self.catalog:
            source = (ROOT / entry["id"] / "Program.cs").read_text()
            if not entry.get("sourceOnly") and ".WithHex1bFlow(" in source and ".WaitForCompletionAsync(" not in source:
                finite.add(entry["id"])
        designated = {entry["id"] for entry in self.catalog if entry.get("capturePolicy") == "until-exit"}
        self.assertEqual(finite, designated)
        for entry in self.catalog:
            if entry.get("capturePolicy"):
                self.assertEqual(entry["capturePolicy"], "until-exit")
                self.assertEqual(entry["actions"], [])

    def test_source_only_samples_have_prerequisites_and_build_only_projects(self):
        expected = {r["id"] for r in migrate.inventory() if r["status"] == "source-only"}
        actual = {entry["id"] for entry in self.catalog if entry.get("sourceOnly")}
        self.assertEqual(expected, actual)
        for entry in self.catalog:
            if not entry.get("sourceOnly"):
                continue
            with self.subTest(sample=entry["id"]):
                self.assertIs(entry["sourceOnly"], True)
                self.assertIsInstance(entry["prerequisites"], str)
                self.assertTrue(entry["prerequisites"].strip())
                self.assertEqual(entry["actions"], [])
                self.assertNotIn("readyText", entry)
                self.assertNotIn("capturePolicy", entry)
                directory = ROOT / entry["id"]
                readme = (directory / "README.md").read_text()
                self.assertIn(entry["prerequisites"], readme)
                self.assertIn("must never execute", readme)
                project = ET.fromstring((directory / "Sample.csproj").read_text())
                self.assertEqual(project.findall(".//Exec"), [])
                self.assertEqual(project.findall(".//Target"), [])
                sdk = "Microsoft.NET.Sdk.Web" if entry["page"] == "guide/using-the-emulator.md" else "Microsoft.NET.Sdk"
                self.assertEqual(project.get("Sdk"), sdk)

    @unittest.skipUnless(os.name == "posix", "PTY readiness probing requires macOS/Linux.")
    def test_smoke_refuses_to_execute_source_only_entries(self):
        from smoke import smoke
        from unittest.mock import patch

        with patch("smoke.subprocess.Popen", side_effect=AssertionError("Unexpected process launch")):
            result = smoke({"id": "source-only-check", "sourceOnly": True})
        self.assertTrue(result["skipped"])
        self.assertTrue(result["success"])

    def test_inventory_excludes_generated_api_member_pages(self):
        references = {record["page"] for record in migrate.inventory() if record["page"].startswith("reference/")}
        self.assertLessEqual(references, {"reference/index.md", "reference/cli.md"})

    @unittest.skipUnless(os.name == "posix", "PTY readiness probing requires macOS/Linux.")
    def test_readiness_probe_ignores_terminal_styling_and_titles(self):
        from smoke import plain_output

        output = b"\x1b]0;Window title\x07\x1b[20;1HFoot\x1b[31mer\x1b[0m"
        self.assertEqual(plain_output(output), "Footer")

    def test_changed_programs_explain_necessary_adaptations(self):
        sources = {(r["page"], r["codeKey"]): r["code"] for r in migrate.inventory()}
        for entry in self.catalog:
            original = sources[(entry["page"], entry["codeKey"])]
            source = (ROOT / entry["id"] / "Program.cs").read_text()
            if original is None or original.strip() != source.strip():
                with self.subTest(sample=entry["id"]):
                    self.assertTrue(entry.get("notes"))
                    readme = (ROOT / entry["id"] / "README.md").read_text()
                    self.assertIn(entry["notes"], readme)

    def test_compatibility_conversion_is_idempotent(self):
        sample = '.WithHex1bApp((app, options) => ctx => ctx.Border(ctx.Text("Test"), title: "Title"))'
        converted, notes = adapt.adapt(sample)
        self.assertIn('(Hex1bApp app) =>', converted)
        self.assertIn('.Title("Title")', converted)
        self.assertTrue(notes)
        self.assertEqual(adapt.adapt(converted), (converted, []))

    def test_template_cooking_preserves_csharp_escapes(self):
        self.assertEqual(migrate.cook(r'ctx.Text("a\\nb")'), r'ctx.Text("a\nb")')
        self.assertEqual(migrate.cook(r'\`value \${x}\`'), '`value ${x}`')

    def test_compatibility_conversion_preserves_markdown_backticks(self):
        code = 'ctx.Markdown("""Use `inline code` here.""")'
        self.assertEqual(adapt.adapt(code), (code, []))
        converted, _ = adapt.adapt('ctx.Text(`Score: ${score}`)')
        self.assertEqual(converted, 'ctx.Text($"Score: {score}")')


if __name__ == "__main__":
    unittest.main()
