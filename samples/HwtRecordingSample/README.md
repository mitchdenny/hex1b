# Pixel postcards

Requires the .NET 10 SDK. From this sample's directory:

```sh
dotnet run
```

Select **Next postcard** and press Enter to change the counter and switch between
daybreak and moonrise. Tab selects **Quit**. Ctrl+C also exits. Use a terminal at
least 72 columns wide and 22 rows tall to see the whole gallery.

The two 320×180 images are generated in `GalleryArtwork.cs`: a KGP mountain
landscape and Sixel ocean bands. Terminals without either graphics capability
show that widget's text fallback. `GalleryState.cs` owns the counter and cached
artwork; `Program.cs` builds the UI. There are no recording or automation hooks.
The Sixel widget is experimental.

The project references the published Hex1b 0.166.0 NuGet package. The first run
restores its dependencies from NuGet; no Hex1b source checkout is required.

Inside the Hex1b source repository, a separate `HwtRecordingDriver` runs this
same compiled app in a PTY and records its terminal state for offline browser
playback. It is not needed to run this sample and is not included in the sample
clone. The website shows and exports the same source files used for recording.
