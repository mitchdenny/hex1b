v.Hyperlink("Click to log", "https://example.com")
 .OnClick(e => Console.WriteLine($"Clicked: {e.Uri}"))