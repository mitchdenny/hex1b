package main

import (
	"os"
	"path/filepath"
	"strconv"
	"testing"
)

func TestSourceSyntaxRetainsUnresolvedOperand(t *testing.T) {
	root, err := os.Getwd()
	if err != nil {
		t.Fatal(err)
	}
	out := sourceSyntax(input{Text: "Type before\nSource missing.tape\nEnter"}, root)
	if out == nil || out.Status != "success" || len(out.Commands) != 3 {
		t.Fatalf("unexpected projection: %+v", out)
	}
	if got := out.Commands[1]; got.Type != "SOURCE" || got.Args != "missing.tape" || got.Options != "" {
		t.Fatalf("unexpected Source command: %+v", got)
	}
}

func TestSourceCyclesAreGuardedBeforeParsing(t *testing.T) {
	root, err := filepath.Abs(filepath.Join(".work", "guard-test-"+strconv.Itoa(os.Getpid())))
	if err != nil {
		t.Fatal(err)
	}
	if err := os.MkdirAll(root, 0o755); err != nil {
		t.Fatal(err)
	}
	defer os.RemoveAll(root)
	for name, text := range map[string]string{"a.tape": "Source b.tape", "b.tape": "Source a.tape"} {
		if err := os.WriteFile(filepath.Join(root, name), []byte(text), 0o644); err != nil {
			t.Fatal(err)
		}
	}
	out := run(input{Text: "Source a.tape"}, root)
	if out.Status != "guarded-source-cycle" || len(out.Commands) != 0 || len(out.Errors) != 0 {
		t.Fatalf("cycle should not invoke the upstream parser: %+v", out)
	}
	if got := safeSources("Source ../outside.tape", root, map[string]bool{}); got != "guarded-external-source" {
		t.Fatalf("external path was not guarded: %s", got)
	}
}
