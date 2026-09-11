// The development-only oracle parses text; it never executes a tape or imports VHS main.
package main

import (
	"crypto/sha256"
	"encoding/json"
	"flag"
	"fmt"
	"go/ast"
	goparser "go/parser"
	gotoken "go/token"
	"os"
	"path/filepath"
	"runtime"
	"sort"
	"strconv"
	"strings"

	"github.com/charmbracelet/vhs/lexer"
	"github.com/charmbracelet/vhs/parser"
	"github.com/charmbracelet/vhs/token"
)

type input struct {
	ID           string            `json:"id"`
	Path         string            `json:"path,omitempty"`
	Text         string            `json:"text,omitempty"`
	Origin       string            `json:"origin"`
	OriginalTest string            `json:"originalTest,omitempty"`
	OriginalLine int               `json:"originalLine,omitempty"`
	Files        map[string]string `json:"files,omitempty"`
	Difference   string            `json:"difference,omitempty"`
	InputSHA256  string            `json:"inputSha256"`
}

type normalizedToken struct {
	Type    string `json:"type"`
	Literal string `json:"literal"`
	Line    int    `json:"line"`
	Column  int    `json:"column"`
}

type command struct {
	Type    string `json:"type"`
	Options string `json:"options"`
	Args    string `json:"args"`
	Source  string `json:"source"`
}

type diagnostic struct {
	Token   normalizedToken `json:"token"`
	Message string          `json:"message"`
}

type result struct {
	input
	Tokens   []normalizedToken `json:"tokens"`
	Commands []command         `json:"commands"`
	Errors   []diagnostic      `json:"errors"`
	Status   string            `json:"status"`
	Syntax   *projection       `json:"syntax,omitempty"`
}

type projection struct {
	Commands []command    `json:"commands"`
	Errors   []diagnostic `json:"errors"`
	Status   string       `json:"status"`
}

func check(err error) {
	if err != nil {
		panic(err)
	}
}

func writeJSON(path string, value any) {
	data, err := json.MarshalIndent(value, "", "  ")
	check(err)
	check(os.WriteFile(path, append(data, '\n'), 0o644))
}

func stringValue(expr ast.Expr) (string, bool) {
	literal, ok := expr.(*ast.BasicLit)
	if !ok || literal.Kind != gotoken.STRING {
		return "", false
	}
	value, err := strconv.Unquote(literal.Value)
	check(err)
	return value, true
}

func extract(path string) []input {
	fs := gotoken.NewFileSet()
	file, err := goparser.ParseFile(fs, path, nil, 0)
	check(err)
	var inputs []input
	for _, decl := range file.Decls {
		fn, ok := decl.(*ast.FuncDecl)
		if !ok || !strings.HasPrefix(fn.Name.Name, "Test") {
			continue
		}
		subtest := ""
		ast.Inspect(fn.Body, func(node ast.Node) bool {
			if call, ok := node.(*ast.CallExpr); ok && len(call.Args) > 0 {
				if selector, ok := call.Fun.(*ast.SelectorExpr); ok && selector.Sel.Name == "Run" {
					subtest, _ = stringValue(call.Args[0])
				}
			}
			add := func(text, name string, position gotoken.Pos, files map[string]string) {
				line := fs.Position(position).Line
				inputs = append(inputs, input{
					ID:   fmt.Sprintf("inline/%s/%s-L%d", filepath.Base(filepath.Dir(path)), fn.Name.Name, line),
					Text: text, Origin: filepath.ToSlash(filepath.Join(filepath.Base(filepath.Dir(path)), filepath.Base(path))),
					OriginalTest: fn.Name.Name + name, OriginalLine: line, Files: files,
				})
			}
			if assign, ok := node.(*ast.AssignStmt); ok && len(assign.Lhs) == 1 && len(assign.Rhs) == 1 {
				if id, ok := assign.Lhs[0].(*ast.Ident); ok && id.Name == "input" {
					if text, ok := stringValue(assign.Rhs[0]); ok {
						add(text, "", assign.Pos(), nil)
					}
				}
			}
			if literal, ok := node.(*ast.CompositeLit); ok {
				fields := map[string]ast.Expr{}
				for _, element := range literal.Elts {
					if kv, ok := element.(*ast.KeyValueExpr); ok {
						if name, ok := kv.Key.(*ast.Ident); ok {
							fields[name.Name] = kv.Value
						}
					}
				}
				if text, ok := stringValue(fields["tape"]); ok {
					name, _ := stringValue(fields["name"])
					if name == "" {
						name = subtest
					}
					var files map[string]string
					if write, ok := fields["writeFile"].(*ast.Ident); ok && write.Name == "true" {
						source, _ := stringValue(fields["srcTape"])
						files = map[string]string{"source.tape": source}
					}
					add(text, "/"+name, literal.Pos(), files)
				}
			}
			return true
		})
	}
	return inputs
}

func normalizeToken(t token.Token) normalizedToken {
	return normalizedToken{string(t.Type), t.Literal, t.Line, t.Column}
}

func tokenize(text string) []normalizedToken {
	tokens := []normalizedToken{}
	l := lexer.New(text)
	for count := 0; count <= len(text)*2+10; count++ {
		t := l.NextToken()
		tokens = append(tokens, normalizeToken(t))
		if t.Type == token.EOF {
			return tokens
		}
	}
	panic("lexer made no progress")
}

// Recursion is checked before calling the unmodified upstream parser: its nested
// Source check runs too late to protect against cycles and stack overflows.
func safeSources(text, root string, active map[string]bool) string {
	tokens := tokenize(text)
	for i, t := range tokens {
		if t.Type != token.SOURCE || i+1 >= len(tokens) || tokens[i+1].Type != token.STRING {
			continue
		}
		path := tokens[i+1].Literal
		if filepath.Ext(path) != ".tape" {
			continue
		}
		absolute := filepath.Join(root, path)
		relative, err := filepath.Rel(root, absolute)
		if err != nil || filepath.IsAbs(path) || relative == ".." || strings.HasPrefix(relative, ".."+string(filepath.Separator)) {
			return "guarded-external-source"
		}
		if active[absolute] {
			return "guarded-source-cycle"
		}
		data, err := os.ReadFile(absolute)
		if err != nil {
			continue
		}
		active[absolute] = true
		status := safeSources(string(data), root, active)
		delete(active, absolute)
		if status != "" {
			return status
		}
	}
	return ""
}

// Pure Hex1b parsing retains Source. Project syntactically valid Source operands
// through the real upstream Env parser (one STRING operand), then restore their
// command identity. The original upstream expansion result is kept separately.
func sourceSyntax(in input, root string) *projection {
	tokens := tokenize(in.Text)
	projected := in.Text
	found := false
	for i := len(tokens) - 2; i >= 0; i-- {
		t := tokens[i]
		if t.Type != token.SOURCE {
			continue
		}
		found = true
		next := tokens[i+1]
		if next.Type != token.STRING || filepath.Ext(next.Literal) != ".tape" {
			continue
		}
		line, column, offset := 1, 1, 0
		for offset < len(in.Text) && (line != t.Line || column != t.Column) {
			if in.Text[offset] == '\n' {
				line++
				column = 1
			} else {
				column++
			}
			offset++
		}
		if !strings.HasPrefix(in.Text[offset:], "Source") {
			panic("Source token position cannot be projected")
		}
		projected = projected[:offset] + "Env Hex1bConformanceSourceMarker" + projected[offset+len("Source"):]
	}
	if !found {
		return nil
	}
	in.Text = projected
	out := run(in, root)
	for i := range out.Commands {
		if out.Commands[i].Type == token.ENV && out.Commands[i].Options == "Hex1bConformanceSourceMarker" {
			out.Commands[i].Type = token.SOURCE
			out.Commands[i].Options = ""
		}
	}
	return &projection{Commands: out.Commands, Errors: out.Errors, Status: out.Status}
}

func run(in input, root string) (out result) {
	out = result{input: in, Commands: []command{}, Errors: []diagnostic{}}
	defer func() {
		if problem := recover(); problem != nil {
			out.Status = "upstream-panic"
			out.Difference = fmt.Sprint(problem)
		}
	}()
	out.Tokens = tokenize(in.Text)
	if status := safeSources(in.Text, root, map[string]bool{}); status != "" {
		out.Status = status
		return out
	}
	old, err := os.Getwd()
	check(err)
	check(os.Chdir(root))
	defer func() { check(os.Chdir(old)) }()
	p := parser.New(lexer.New(in.Text))
	for _, c := range p.Parse() {
		out.Commands = append(out.Commands, command{string(c.Type), c.Options, c.Args, c.Source})
	}
	for _, e := range p.Errors() {
		out.Errors = append(out.Errors, diagnostic{normalizeToken(e.Token), e.Msg})
	}
	out.Status = "success"
	if len(out.Errors) > 0 {
		out.Status = "error"
	}
	return out
}

func main() {
	dataFlag := flag.String("data", "../../tests/Hex1b.Tests/TestData/Tape", "offline corpus directory")
	flag.Parse()
	if runtime.Version() != "go1.25.12" {
		panic("regenerate with GOTOOLCHAIN=go1.25.12 (got " + runtime.Version() + ")")
	}
	data, err := filepath.Abs(*dataFlag)
	check(err)
	var inputs []input
	check(filepath.WalkDir(filepath.Join(data, "upstream"), func(path string, entry os.DirEntry, err error) error {
		check(err)
		if entry.IsDir() || !strings.HasSuffix(path, ".tape") {
			return nil
		}
		content, err := os.ReadFile(path)
		check(err)
		relative, err := filepath.Rel(filepath.Join(data, "upstream"), path)
		check(err)
		inputs = append(inputs, input{ID: "upstream/" + filepath.ToSlash(relative),
			Path: "upstream/" + filepath.ToSlash(relative), Text: string(content), Origin: filepath.ToSlash(relative)})
		return nil
	}))
	for _, path := range []string{".work/upstream/lexer/lexer_test.go", ".work/upstream/parser/parser_test.go"} {
		inputs = append(inputs, extract(path)...)
	}
	supplemental, err := os.ReadFile("supplemental.json")
	check(err)
	var cases []input
	check(json.Unmarshal(supplemental, &cases))
	inputs = append(inputs, cases...)
	for _, keyword := range []string{
		"Backspace", "Delete", "Insert", "Enter", "Escape", "Tab", "Space",
		"Down", "Left", "Right", "Up", "PageUp", "PageDown", "ScrollUp", "ScrollDown",
	} {
		inputs = append(inputs, input{ID: "supplemental/command-invalid/" + keyword,
			Origin: "Hex1b", Text: keyword + "@"})
	}
	for keyword, text := range map[string]string{
		"Alt": "Alt+", "Ctrl": "Ctrl+", "Shift": "Shift+", "Copy": "Copy",
		"Env": "Env NAME", "Hide": "Hide 2", "Output": "Output", "Paste": "Paste 2",
		"Require": "Require", "Screenshot": "Screenshot", "Set": "Set Unknown 10",
		"Show": "Show 2", "Sleep": "Sleep", "Source": "Source", "Type": "Type",
		"Wait": "Wait+Window",
	} {
		inputs = append(inputs, input{ID: "supplemental/command-invalid/" + keyword,
			Origin: "Hex1b", Text: text})
	}
	for keyword, value := range token.Keywords {
		if token.IsSetting(value) {
			inputs = append(inputs, input{ID: "supplemental/setting-invalid/" + keyword,
				Origin: "Hex1b", Text: "Set " + keyword + " 1px"})
		}
	}
	sort.Slice(inputs, func(i, j int) bool { return inputs[i].ID < inputs[j].ID })
	var results []result
	counts := map[string]int{}
	for index, in := range inputs {
		in.InputSHA256 = fmt.Sprintf("%x", sha256.Sum256([]byte(in.Text)))
		root := filepath.Join(data, "upstream")
		if !strings.HasPrefix(in.ID, "upstream/") {
			root, err = filepath.Abs(filepath.Join(".work", "cases", strconv.Itoa(index)))
			check(err)
			check(os.RemoveAll(root))
			check(os.MkdirAll(root, 0o755))
			for name, content := range in.Files {
				if filepath.IsAbs(name) || strings.Contains(name, "..") {
					panic("unsafe fixture path")
				}
				path := filepath.Join(root, name)
				check(os.MkdirAll(filepath.Dir(path), 0o755))
				check(os.WriteFile(path, []byte(content), 0o644))
			}
		}
		out := run(in, root)
		out.Syntax = sourceSyntax(in, root)
		if out.Path != "" {
			out.Text = ""
		}
		results = append(results, out)
		counts[out.Status]++
	}
	sort.Slice(results, func(i, j int) bool { return results[i].ID < results[j].ID })
	settings := []string{}
	for keyword, value := range token.Keywords {
		if token.IsSetting(value) {
			settings = append(settings, keyword)
		}
	}
	sort.Strings(settings)
	// Derive dispatch completeness from the actual parseCommand switch, not the
	// upstream CommandTypes list (which omits SHIFT and includes ILLEGAL).
	fs := gotoken.NewFileSet()
	file, err := goparser.ParseFile(fs, ".work/upstream/parser/parser.go", nil, 0)
	check(err)
	commands := []string{}
	for _, decl := range file.Decls {
		if fn, ok := decl.(*ast.FuncDecl); ok && fn.Name.Name == "parseCommand" {
			ast.Inspect(fn.Body, func(n ast.Node) bool {
				if branch, ok := n.(*ast.CaseClause); ok {
					for _, expr := range branch.List {
						if selector, ok := expr.(*ast.SelectorExpr); ok {
							commands = append(commands, selector.Sel.Name)
						}
					}
				}
				return true
			})
		}
	}
	sort.Strings(commands)
	writeJSON(filepath.Join(data, "expectations.json"), map[string]any{
		"schemaVersion": 1, "ref": "c073383b5de0b1f57bf514113029c306bc986539",
		"goVersion": runtime.Version(), "commandInventory": commands, "settingInventory": settings,
		"cases": results,
	})
	fmt.Printf("Generated %d cases, %d commands, %d settings: %v\n", len(results), len(commands), len(settings), counts)
}
