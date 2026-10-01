a.Section(s => [...])
  .Title("SOURCE CONTROL")
  .LeftActions(la => [
      la.Toggle("▶", "▼"),
      la.Icon("✓").OnClick(ctx => Commit()),
  ])
