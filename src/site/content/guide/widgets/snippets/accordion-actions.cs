a.Section(s => [...])
  .Title("EXPLORER")
  .RightActions(ra => [
      ra.Icon("+").OnClick(ctx => CreateFile()),
      ra.Icon("⟳").OnClick(ctx => Refresh()),
  ])
