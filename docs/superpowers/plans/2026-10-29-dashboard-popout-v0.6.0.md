# Dashboard Pop-out v0.6.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the dashboard as one resizable pop-out window with material backgrounds and a per-gauge Analog/Digital face, in portable v0.6.0 zips.

**Architecture:** `MainViewModel` keeps the one `DashboardViewModel` and the one poll loop. A new `DashboardWindow` is a view over that instance: the window `DataContext` stays `MainViewModel`, and the moved dashboard surface keeps `DataContext="{Binding Dashboard}"` so Connect still resolves and no second view model or engine is created. Pure rules for background ids, gauge face, poll gating, status text, and window placement live in `ObdLink.Core` and are unit-tested. The WPF window only calls them.

**Tech Stack:** C#/.NET 8 WPF, existing ObdLink.App / ObdLink.Core patterns.

## Global Constraints

- Version 0.6.0 when shipping the feature PR (this plan PR stays docs-only).
- Portable, no installer; all writes under app Data\.
- American spelling: tire, canceled, aluminum.
- win-x64 + ARM64-only zips.
- High-quality non-tiled backgrounds (UniformToFill, TileMode.None).
- Never leave a bugfix PR stranded across a version bump.

**Spec:** `docs/superpowers/specs/2026-10-29-dashboard-popout-v0.6.0-design.md`

**Where these tasks run:** On a new branch cut from `master` after this docs pull request merges. The commit steps below belong to that feature branch. Do not implement them in the docs pull request. `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo` must pass at the end of every task. The test project targets `net8.0` (no WPF), so window behavior is locked with core functions plus source assertions. A Windows pass of the numbered checks in the spec is the last step of Task 8.

---

## File Structure

| File | Responsibility |
| --- | --- |
| Create: `src/ObdLink.Core/Dashboard/GaugeFaces.cs` | `Analog` / `Digital` strings, `Normalize`, `ShowsNeedle` |
| Create: `src/ObdLink.Core/Dashboard/DashboardSession.cs` | `ShouldRun`, `PollAllowed`, `StateText` |
| Create: `src/ObdLink.Core/Dashboard/DashboardWindowPlacement.cs` | DIP placement `Resolve` |
| Create: `src/ObdLink.App/MonitorAreas.cs` | Win32 monitor working areas converted to DIPs |
| Create: `src/ObdLink.App/DashboardWindow.xaml` | Pop-out shell (moved dashboard surface, Keep live, face combo) |
| Create: `src/ObdLink.App/DashboardWindow.xaml.cs` | Singleton partner: open, close, placement, drag/drop moved here |
| Create: `src/ObdLink.App/Themes/Dashboard.xaml` | Dashboard data templates moved out of `MainWindow.xaml` so both windows can use them |
| Create: `tools/GenerateBackgrounds/generate.py` | Writes the six MIT-owned material JPEGs |
| Create: `tests/ObdLink.Core.Tests/V060Tests.cs` | v0.6.0 core and source-contract tests |
| Modify: `src/ObdLink.Core/Dashboard/DashboardBackgrounds.cs` | New material ids, cabin ids normalize to `None` |
| Modify: `src/ObdLink.Core/Dashboard/DashboardLayout.cs` | `DashItem.Face` and `Clone` |
| Modify: `src/ObdLink.Core/Safety/AppSettings.cs` | `DashboardOptions` placement and Keep live; `Normalize` faces and non-finite bounds |
| Modify: `src/ObdLink.Core/Ui/Shortcuts.cs` | Pages without Dashboard, `PageByNumber`, `Ctrl+Shift+G` |
| Modify: `src/ObdLink.App/DashboardViewModel.cs` | Window-open polling, Keep live, face, status, `Stop` |
| Modify: `src/ObdLink.App/MainViewModel.cs` | Default tab Modules, remap `"Dashboard"`, `OpenDashboardCommand` |
| Modify: `src/ObdLink.App/MainWindow.xaml` | Remove Dashboard tab and Tools item; add View item and header button |
| Modify: `src/ObdLink.App/MainWindow.xaml.cs` | Open/focus/close the one window; Alt+9 guard; drag handlers leave this file |
| Modify: `src/ObdLink.App/Controls/Gauges.cs` | `CircularGauge.ShowAnalog` |
| Modify: `src/ObdLink.App/App.xaml` | Merge `Themes/Dashboard.xaml` |
| Modify: `assets/backgrounds/PROVENANCE.txt` | Provenance for the six materials |
| Delete: `assets/backgrounds/{analog,digital-dash,digital-cluster,highway,night,leather,dusk,center-stack}.jpg` | v0.5.2 cabin photos |
| Add: `assets/backgrounds/{wood-grain,brushed-aluminum,gloss-black,diamond-plate,carbon-fiber,sports-leather}.jpg` | Material photos |
| Modify: `tests/ObdLink.Core.Tests/V040Tests.cs` | 8 pages; cabin id assertions |
| Modify: `tests/ObdLink.Core.Tests/V033Tests.cs` | Drag markup now lives in the dashboard templates and window |
| Modify: `tests/ObdLink.Core.Tests/V052Tests.cs` | Background contract, then version 0.6.0 in Task 8 |
| Modify: `docs/GUIDE.md`, `docs/RELEASE-NOTES.md`, `README.md` | User-facing v0.6.0 description |
| Modify: `src/ObdLink.App/ObdLink.App.csproj`, `src/ObdLink.Core/ObdLink.Core.csproj` | `<Version>0.6.0</Version>` in Task 8 only |
| Modify: `.gitignore` | `artifacts/` so publish zips are not committed |

`src/ObdLink.App/DashboardBackdrop.cs` stays on `UniformToFill` and `TileMode.None`. The csproj glob `assets\backgrounds\*.jpg` already embeds every JPEG in that folder.

## Requirement map

| Spec requirement | Task |
| --- | --- |
| Remove Dashboard from the side nav; Modules is the default page; Alt+1 is Modules; Alt+9 does not throw | Task 4 |
| One `DashboardWindow`, opened from View → Dashboard, the header button, and Ctrl+Shift+G; focus if open | Task 4 |
| Close pauses polling unless Keep live; user pause survives close and reopen; one poll loop | Task 3 (rules), Task 4 (wiring) |
| Persist Keep live and window bounds under `Data\settings.json` | Task 3 (fields and `Resolve`), Task 4 (Keep live checkbox), Task 6 (save/restore) |
| Six materials, Custom, None, cabin ids normalize to None, provenance, cover stretch | Task 1 |
| Per-widget Analog/Digital on needle-capable Dial bodies; text-only widgets unchanged; saved on `DashItem` | Task 2 (model), Task 5 (drawing and combo) |
| Guide, release notes, tooltips | Task 4 (in-app strings), Task 7 (docs) |
| Version 0.6.0, scan open PRs, win-x64 and ARM64 zips | Task 8 |

---

### Task 1: Material backgrounds and cabin-id migration

**Files:**
- Create: `tools/GenerateBackgrounds/generate.py`
- Create: `tests/ObdLink.Core.Tests/V060Tests.cs`
- Modify: `src/ObdLink.Core/Dashboard/DashboardBackgrounds.cs`
- Modify: `assets/backgrounds/PROVENANCE.txt`
- Modify: `tests/ObdLink.Core.Tests/V040Tests.cs:162-197`
- Modify: `tests/ObdLink.Core.Tests/V052Tests.cs:36-80`
- Delete: the eight cabin JPEGs listed above
- Add: the six material JPEGs listed above

**Interfaces:**
- Consumes: `AppSettings.Normalize` already calls `DashboardBackgrounds.Normalize` (`src/ObdLink.Core/Safety/AppSettings.cs` around the `Dashboard.Background = ...Normalize` line). `DashboardBackdrop.For` calls `DashboardBackdropPolicy.FileFor`.
- Produces:
  - `DashboardBackgrounds.None`, `WoodGrain`, `BrushedAluminum`, `GlossBlack`, `DiamondPlate`, `CarbonFiber`, `SportsLeather`, `Custom` (`string` constants)
  - `DashboardBackgrounds.Choices` (`IReadOnlyList<DashboardBackgroundChoice>`) in the order None, WoodGrain, BrushedAluminum, GlossBlack, DiamondPlate, CarbonFiber, SportsLeather, Custom
  - `DashboardBackgrounds.Normalize(string? id) -> string` returns a canonical v0.6.0 id, or `None` for null, empty, unknown, and every v0.5.2 cabin id (`Analog`, `Wood`, `Steel`, `Mirror`, `Onyx`, `Leather`, `Carbon`, `Brushed`), case-insensitive
  - `DashboardBackdropPolicy.FileFor(string? id) -> string?` returns the JPEG file name for the six materials and null for `None` and `Custom`
  - `DashboardBackdropPolicy.TileBuiltIns` stays `false`, `CoverStretch` stays `"UniformToFill"`, `ScrimAlpha` stays `0x48`

- [ ] **Step 1: Write the failing tests**

Create `tests/ObdLink.Core.Tests/V060Tests.cs`:

```csharp
using ObdLink.Core.Dashboard;
using ObdLink.Core.Safety;
using Xunit;

namespace ObdLink.Core.Tests;

public class V060Tests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(me)!, "..", ".."));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nope")]
    [InlineData("Analog")]
    [InlineData("wood")]
    [InlineData("STEEL")]
    [InlineData("Mirror")]
    [InlineData("onyx")]
    [InlineData("Leather")]
    [InlineData("Carbon")]
    [InlineData("Brushed")]
    public void Removed_cabin_ids_and_unknown_ids_normalize_to_none(string? id) =>
        Assert.Equal(DashboardBackgrounds.None, DashboardBackgrounds.Normalize(id));

    [Fact]
    public void Material_ids_round_trip_and_custom_stays_custom()
    {
        Assert.Equal(DashboardBackgrounds.WoodGrain, DashboardBackgrounds.Normalize("woodgrain"));
        Assert.Equal(DashboardBackgrounds.BrushedAluminum, DashboardBackgrounds.Normalize("brushedaluminum"));
        Assert.Equal(DashboardBackgrounds.GlossBlack, DashboardBackgrounds.Normalize("glossblack"));
        Assert.Equal(DashboardBackgrounds.DiamondPlate, DashboardBackgrounds.Normalize("diamondplate"));
        Assert.Equal(DashboardBackgrounds.CarbonFiber, DashboardBackgrounds.Normalize("carbonfiber"));
        Assert.Equal(DashboardBackgrounds.SportsLeather, DashboardBackgrounds.Normalize("sportsleather"));
        Assert.Equal(DashboardBackgrounds.Custom, DashboardBackgrounds.Normalize("custom"));
        Assert.Equal(DashboardBackgrounds.None, DashboardBackgrounds.Normalize("none"));
    }

    [Fact]
    public void Saved_analog_background_loads_as_none_and_keeps_layouts()
    {
        var path = Path.Combine(Path.GetTempPath(), "v060-" + Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"Dashboard":{"Background":"Analog","Layouts":[{"Name":"Default","Items":[{"Key":"rpm","Display":"Dial","Size":"Large"}]}]}}""");
        var loaded = AppSettings.Load(path);
        Assert.Equal(DashboardBackgrounds.None, loaded.Dashboard.Background);
        Assert.Equal("rpm", loaded.Dashboard.Current().Items[0].Key);
    }

    [Fact]
    public void Material_jpegs_cover_and_cabin_files_are_gone()
    {
        Assert.False(DashboardBackdropPolicy.TileBuiltIns);
        Assert.Equal("UniformToFill", DashboardBackdropPolicy.CoverStretch);
        Assert.Equal((byte)0x48, DashboardBackdropPolicy.ScrimAlpha);
        Assert.Null(DashboardBackdropPolicy.FileFor(DashboardBackgrounds.None));
        Assert.Null(DashboardBackdropPolicy.FileFor(DashboardBackgrounds.Custom));
        Assert.Equal("wood-grain.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.WoodGrain));
        Assert.Equal("brushed-aluminum.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.BrushedAluminum));
        Assert.Equal("gloss-black.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.GlossBlack));
        Assert.Equal("diamond-plate.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.DiamondPlate));
        Assert.Equal("carbon-fiber.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.CarbonFiber));
        Assert.Equal("sports-leather.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.SportsLeather));

        var names = DashboardBackgrounds.Choices.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "None (app theme)", "Fine wood grain", "Brushed aluminum", "High-gloss black", "Diamond-plate metal", "Carbon fiber", "Sports-car leather", "Custom image" }, names);

        var dir = Path.Combine(Root(), "assets", "backgrounds");
        foreach (var gone in new[] { "analog.jpg", "digital-dash.jpg", "digital-cluster.jpg", "highway.jpg", "night.jpg", "leather.jpg", "dusk.jpg", "center-stack.jpg" })
            Assert.False(File.Exists(Path.Combine(dir, gone)), gone);
        foreach (var file in new[] { "wood-grain.jpg", "brushed-aluminum.jpg", "gloss-black.jpg", "diamond-plate.jpg", "carbon-fiber.jpg", "sports-leather.jpg" })
        {
            var path = Path.Combine(dir, file);
            Assert.True(File.Exists(path), path);
            var (w, h) = JpegSize(path);
            Assert.True(w >= 1920 && h >= 1080, $"{file} is {w}x{h}");
            Assert.InRange(new FileInfo(path).Length, 40_000, 1_500_000);
        }
        var prov = File.ReadAllText(Path.Combine(dir, "PROVENANCE.txt"));
        Assert.Contains("generated", prov, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIT", prov);
        Assert.Contains("wood-grain.jpg", prov);
        Assert.Contains("brushed-aluminum.jpg", prov);
        Assert.Contains("gloss-black.jpg", prov);
        Assert.Contains("diamond-plate.jpg", prov);
        Assert.Contains("carbon-fiber.jpg", prov);
        Assert.Contains("sports-leather.jpg", prov);
        Assert.Contains("material surfaces", prov);
        Assert.Contains("not photographs of a vehicle cabin", prov);
        var backdrop = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "DashboardBackdrop.cs"));
        Assert.Contains("TileMode.None", backdrop);
        Assert.Contains("UniformToFill", backdrop);
        Assert.DoesNotContain("TileMode.Tile", backdrop);
    }

    private static (int Width, int Height) JpegSize(string path)
    {
        using var fs = File.OpenRead(path);
        Span<byte> buf = stackalloc byte[8];
        if (fs.Read(buf[..2]) != 2 || buf[0] != 0xFF || buf[1] != 0xD8)
            throw new InvalidDataException(path + " is not a JPEG");
        while (fs.Position + 4 < fs.Length)
        {
            int b = fs.ReadByte();
            if (b != 0xFF) continue;
            int marker;
            do { marker = fs.ReadByte(); } while (marker == 0xFF);
            if (marker is 0xD8 or 0xD9 or (>= 0xD0 and <= 0xD7)) continue;
            if (fs.Read(buf[..2]) != 2) break;
            int len = (buf[0] << 8) | buf[1];
            if (len < 2) break;
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                if (fs.Read(buf) != 8) break;
                int h = (buf[1] << 8) | buf[2];
                int w = (buf[3] << 8) | buf[4];
                return (w, h);
            }
            fs.Seek(len - 2, SeekOrigin.Current);
        }
        throw new InvalidDataException("No JPEG frame in " + path);
    }
}
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo --filter "FullyQualifiedName~V060Tests"`

Expected: FAIL. `DashboardBackgrounds.WoodGrain` (and the other new constants) do not exist.

- [ ] **Step 3: Replace the background catalog**

Replace the constants, `Choices`, `Normalize`, and `DashboardBackdropPolicy.Assets` in `src/ObdLink.Core/Dashboard/DashboardBackgrounds.cs` with:

```csharp
public const string None = "None";
public const string WoodGrain = "WoodGrain";
public const string BrushedAluminum = "BrushedAluminum";
public const string GlossBlack = "GlossBlack";
public const string DiamondPlate = "DiamondPlate";
public const string CarbonFiber = "CarbonFiber";
public const string SportsLeather = "SportsLeather";
public const string Custom = "Custom";

public static readonly IReadOnlyList<DashboardBackgroundChoice> Choices = new DashboardBackgroundChoice[]
{
    new(None, "None (app theme)"),
    new(WoodGrain, "Fine wood grain"),
    new(BrushedAluminum, "Brushed aluminum"),
    new(GlossBlack, "High-gloss black"),
    new(DiamondPlate, "Diamond-plate metal"),
    new(CarbonFiber, "Carbon fiber"),
    new(SportsLeather, "Sports-car leather"),
    new(Custom, "Custom image"),
};

public static string Normalize(string? id) =>
    Choices.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) is { } hit ? hit.Id : None;
```

```csharp
public static readonly IReadOnlyList<BackdropAsset> Assets = new BackdropAsset[]
{
    new(DashboardBackgrounds.WoodGrain, "wood-grain.jpg"),
    new(DashboardBackgrounds.BrushedAluminum, "brushed-aluminum.jpg"),
    new(DashboardBackgrounds.GlossBlack, "gloss-black.jpg"),
    new(DashboardBackgrounds.DiamondPlate, "diamond-plate.jpg"),
    new(DashboardBackgrounds.CarbonFiber, "carbon-fiber.jpg"),
    new(DashboardBackgrounds.SportsLeather, "sports-leather.jpg"),
};
```

Delete the v0.5.2 constants `Wood`, `Steel`, `Mirror`, `Onyx`, `Leather`, `Carbon`, `Brushed`, and `Analog`. Leave `TileBuiltIns`, `CoverStretch`, `ScrimAlpha`, and `FileFor` as they are. `DashboardBackdrop.cs` needs no edit.

- [ ] **Step 4: Generate the six JPEGs and write provenance**

Create `tools/GenerateBackgrounds/generate.py` exactly as follows. It writes six baseline 1920×1080 JPEGs into `assets/backgrounds/` and retries JPEG quality until each file is between 40,000 and 1,500,000 bytes.

```python
#!/usr/bin/env python3
"""Seeded material backgrounds for OBDLink Diag v0.6.0. Not a vehicle cabin."""
import math
import random
from pathlib import Path
from PIL import Image

W, H = 1920, 1080
ROOT = Path(__file__).resolve().parents[2] / "assets" / "backgrounds"
RNG = random.Random(6060)

def field():
    return [RNG.randrange(256) for _ in range(W * H)]

def save(name, img):
    path = ROOT / name
    for quality in (82, 72, 62, 90, 52):
        img.save(path, format="JPEG", quality=quality, optimize=True, progressive=False)
        size = path.stat().st_size
        if 40_000 <= size <= 1_500_000:
            print(f"{path} {size} q={quality}")
            return
    raise SystemExit(f"{name} is {path.stat().st_size} bytes, outside 40000..1500000")

def paint(fn):
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        for x in range(W):
            px[x, y] = fn(x, y)
    return img

def wood():
    n = field()
    def fn(x, y):
        g = n[y * W + x]
        band = int(22 * (1 + math.sin(y / 34.0)))
        return (96 + band + g // 4, 62 + band // 2 + g // 6, 34 + g // 9)
    save("wood-grain.jpg", paint(fn))

def aluminum():
    n = field()
    def fn(x, y):
        g = n[y * W + x] // 8
        streak = 8 if (y + g) % 3 == 0 else 0
        v = 150 + streak + (g // 2)
        return (v, v, v + 4)
    save("brushed-aluminum.jpg", paint(fn))

def gloss():
    n = field()
    def fn(x, y):
        dx, dy = x - 480, y - 180
        shine = max(0, 70 - int(math.sqrt(dx * dx + dy * dy) / 8))
        v = 8 + shine + n[y * W + x] // 32
        return (v, v, min(255, v + 2))
    save("gloss-black.jpg", paint(fn))

def diamond():
    def fn(x, y):
        col, row = 28, 16
        cx, cy = (x % col) - col / 2, (y % row) - row / 2
        if (x // col + y // row) % 2:
            cy = (y % row) - row / 2
        d = abs(cx) / (col / 2) + abs(cy) / (row / 2)
        v = 70 + int(40 * max(0, 1 - d))
        return (v, v, v + 3)
    save("diamond-plate.jpg", paint(fn))

def carbon():
    n = field()
    def fn(x, y):
        cell = 10
        ix, iy = (x // cell) % 4, (y // cell) % 4
        on = (ix + iy) % 4 in (0, 1)
        v = (28 if on else 12) + n[y * W + x] // 20
        return (v, v, v)
    save("carbon-fiber.jpg", paint(fn))

def leather():
    n = field()
    def fn(x, y):
        g = n[y * W + x]
        bump = 18 if (x % 14 < 4 and y % 11 < 3) else 0
        v = 46 + bump + g // 16
        return (v + 12, v, v - 6)
    save("sports-leather.jpg", paint(fn))

if __name__ == "__main__":
    ROOT.mkdir(parents=True, exist_ok=True)
    wood(); aluminum(); gloss(); diamond(); carbon(); leather()
```

Install and run:

```bash
python3 -m pip install pillow
python3 tools/GenerateBackgrounds/generate.py
```

Expected: the script prints six paths. Each file is a baseline JPEG, at least 1920×1080, and between 40,000 and 1,500,000 bytes. If a file falls outside that range, change that image's JPEG quality and run the script again until all six are inside the range.

Delete the cabin files:

```bash
rm -f assets/backgrounds/analog.jpg assets/backgrounds/digital-dash.jpg assets/backgrounds/digital-cluster.jpg assets/backgrounds/highway.jpg assets/backgrounds/night.jpg assets/backgrounds/leather.jpg assets/backgrounds/dusk.jpg assets/backgrounds/center-stack.jpg
```

Replace `assets/backgrounds/PROVENANCE.txt` with:

```text
Dashboard background photographs
Generated for OBDLink Diag v0.6.0 by tools/GenerateBackgrounds/generate.py.

These JPEG files were generated for this app. They are original generated
pictures of material surfaces. They are not photographs of a vehicle cabin,
not Ford Motor Company marketing photos, and not files copied from the web.
They ship with this project under the MIT license (see LICENSE).

These files contain no vehicle badges or wordmarks.

Each file is a 1920x1080 baseline JPEG. The app draws each photo once with
cover stretch (UniformToFill) and a light dark scrim. The photos are not tiled.

wood-grain.jpg
  Display name: Fine wood grain
  How obtained: image generated for this app by tools/GenerateBackgrounds/generate.py
  Author: OBDLink Diag project (Dale Wilcox)
  License: MIT. Redistribution inside this MIT-licensed project is allowed.

brushed-aluminum.jpg
  Display name: Brushed aluminum
  How obtained: image generated for this app by tools/GenerateBackgrounds/generate.py
  Author: OBDLink Diag project (Dale Wilcox)
  License: MIT. Redistribution inside this MIT-licensed project is allowed.

gloss-black.jpg
  Display name: High-gloss black
  How obtained: image generated for this app by tools/GenerateBackgrounds/generate.py
  Author: OBDLink Diag project (Dale Wilcox)
  License: MIT. Redistribution inside this MIT-licensed project is allowed.

diamond-plate.jpg
  Display name: Diamond-plate metal
  How obtained: image generated for this app by tools/GenerateBackgrounds/generate.py
  Author: OBDLink Diag project (Dale Wilcox)
  License: MIT. Redistribution inside this MIT-licensed project is allowed.

carbon-fiber.jpg
  Display name: Carbon fiber
  How obtained: image generated for this app by tools/GenerateBackgrounds/generate.py
  Author: OBDLink Diag project (Dale Wilcox)
  License: MIT. Redistribution inside this MIT-licensed project is allowed.

sports-leather.jpg
  Display name: Sports-car leather
  How obtained: image generated for this app by tools/GenerateBackgrounds/generate.py
  Author: OBDLink Diag project (Dale Wilcox)
  License: MIT. Redistribution inside this MIT-licensed project is allowed.
```

The provenance text above is the file to write. It says the pictures are material surfaces and that they are not a vehicle cabin.

- [ ] **Step 5: Update the v0.4.0 and v0.5.2 tests that still expect cabin ids**

In `tests/ObdLink.Core.Tests/V040Tests.cs` `Background_choice_persists_and_custom_images_stay_inside_data`:

- Replace both `Assert.Equal(DashboardBackgrounds.Wood, ...)` calls with `Assert.Equal(DashboardBackgrounds.None, ...)`. The saved value in that test is the string `"wood"`, which is now a removed cabin id.
- Replace the Leather/Onyx choice asserts at the end of that test with:

```csharp
Assert.Contains(DashboardBackgrounds.Choices, c => c.Id == DashboardBackgrounds.SportsLeather && c.Name.Contains("leather"));
Assert.Contains(DashboardBackgrounds.Choices, c => c.Id == DashboardBackgrounds.GlossBlack);
```

In `tests/ObdLink.Core.Tests/V052Tests.cs`, delete the method `Built_in_backgrounds_are_cover_photos_not_tiles`. Task 1's `Material_jpegs_cover_and_cabin_files_are_gone` replaces it. Leave `Version_is_0_5_2` unchanged in this task. It still requires the guide and release notes to mention the v0.5.2 cabin names until Tasks 7 and 8.

- [ ] **Step 6: Run the suite**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo`

Expected: PASS. `Version_is_0_5_2` still passes because `<Version>` is still `0.5.2`.

- [ ] **Step 7: Commit**

```bash
git add src/ObdLink.Core/Dashboard/DashboardBackgrounds.cs assets/backgrounds tools/GenerateBackgrounds/generate.py tests/ObdLink.Core.Tests/V060Tests.cs tests/ObdLink.Core.Tests/V040Tests.cs tests/ObdLink.Core.Tests/V052Tests.cs
git commit -m "feat: replace cabin dashboard backgrounds with material photos"
```

---

### Task 2: Persist Analog/Digital on each layout item

**Files:**
- Create: `src/ObdLink.Core/Dashboard/GaugeFaces.cs`
- Modify: `src/ObdLink.Core/Dashboard/DashboardLayout.cs:11-16`
- Modify: `src/ObdLink.Core/Safety/AppSettings.cs:66`
- Test: `tests/ObdLink.Core.Tests/V060Tests.cs`

**Interfaces:**
- Consumes: `DashItem` (`Key`, `Display`, `Size`, `Clone`). `AppSettings.Normalize` already walks `Dashboard.Layouts`.
- Produces:
  - `GaugeFaces.Analog` = `"Analog"`, `GaugeFaces.Digital` = `"Digital"`
  - `GaugeFaces.Normalize(string? face) -> string` returns `Digital` only for a case-insensitive `"Digital"`; every other value, including null and empty, returns `Analog`
  - `GaugeFaces.ShowsNeedle(DisplayKind display, string? face) -> bool` is true only when `display == DisplayKind.Dial` and `Normalize(face) == Analog`
  - `DashItem.Face` (`string`, default `GaugeFaces.Analog`)
  - `DashItem.Clone()` copies `Face`

- [ ] **Step 1: Write the failing tests**

Append to `V060Tests`:

```csharp
[Fact]
public void Face_defaults_to_analog_and_unknown_values_do_not_drop_the_settings_file()
{
    var path = Path.Combine(Path.GetTempPath(), "v060-face-" + Guid.NewGuid().ToString("N"), "settings.json");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, """{"Dashboard":{"Layouts":[{"Name":"Default","Items":[{"Key":"rpm","Display":"Dial","Size":"Large"},{"Key":"speed","Display":"Dial","Size":"Large","Face":"Digital"},{"Key":"fuel","Display":"Bar","Face":"nope"}]}]}}""");
    var loaded = AppSettings.Load(path);
    var items = loaded.Dashboard.Current().Items;
    Assert.Equal(GaugeFaces.Analog, items[0].Face);
    Assert.Equal(GaugeFaces.Digital, items[1].Face);
    Assert.Equal(GaugeFaces.Analog, items[2].Face);
    Assert.Equal("rpm", items[0].Key);
    var clone = items[1].Clone();
    Assert.Equal(GaugeFaces.Digital, clone.Face);
    Assert.True(GaugeFaces.ShowsNeedle(DisplayKind.Dial, GaugeFaces.Analog));
    Assert.False(GaugeFaces.ShowsNeedle(DisplayKind.Dial, GaugeFaces.Digital));
    Assert.False(GaugeFaces.ShowsNeedle(DisplayKind.Bar, GaugeFaces.Analog));
    Assert.False(GaugeFaces.ShowsNeedle(DisplayKind.Tile, GaugeFaces.Digital));
    Assert.False(GaugeFaces.ShowsNeedle(DisplayKind.Lamp, null));
    Assert.False(GaugeFaces.ShowsNeedle(DisplayKind.Tpms, GaugeFaces.Analog));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo --filter "FullyQualifiedName~Face_defaults_to_analog"`

Expected: FAIL. `GaugeFaces` does not exist.

- [ ] **Step 3: Add the face type and store it on the item**

Create `src/ObdLink.Core/Dashboard/GaugeFaces.cs`:

```csharp
namespace ObdLink.Core.Dashboard;

public static class GaugeFaces
{
    public const string Analog = "Analog";
    public const string Digital = "Digital";

    public static string Normalize(string? face) =>
        string.Equals(face, Digital, StringComparison.OrdinalIgnoreCase) ? Digital : Analog;

    public static bool ShowsNeedle(DisplayKind display, string? face) =>
        display == DisplayKind.Dial && Normalize(face) == Analog;
}
```

Change `DashItem` in `DashboardLayout.cs`:

```csharp
public sealed class DashItem
{
    public string Key { get; set; } = "";
    public DisplayKind Display { get; set; } = DisplayKind.Bar;
    public ItemSize Size { get; set; } = ItemSize.Small;
    public string Face { get; set; } = GaugeFaces.Analog;
    public DashItem Clone() => new() { Key = Key, Display = Display, Size = Size, Face = Face };
}
```

In `AppSettings.Normalize`, extend the layout-item loop so every surviving item gets a legal face. Replace the single `foreach (var l in Dashboard.Layouts)` line with:

```csharp
foreach (var l in Dashboard.Layouts)
{
    l.Items ??= new();
    l.Items.RemoveAll(i => i is null || string.IsNullOrWhiteSpace(i.Key) || ObdLink.Core.Dashboard.TpmsKeys.Retired.Contains(i.Key));
    foreach (var i in l.Items)
        i.Face = ObdLink.Core.Dashboard.GaugeFaces.Normalize(i.Face);
}
```

`Face` is a string on purpose. An unknown token must not throw out of `JsonSerializer` and replace the whole settings file.

- [ ] **Step 4: Run the suite**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo`

Expected: PASS. `DashboardFlow` metrics do not read `Face`, so reorder and flow tests stay green.

- [ ] **Step 5: Commit**

```bash
git add src/ObdLink.Core/Dashboard/GaugeFaces.cs src/ObdLink.Core/Dashboard/DashboardLayout.cs src/ObdLink.Core/Safety/AppSettings.cs tests/ObdLink.Core.Tests/V060Tests.cs
git commit -m "feat: save an analog or digital face on each dashboard item"
```

---

### Task 3: Poll gating, status text, and placement math

**Files:**
- Create: `src/ObdLink.Core/Dashboard/DashboardSession.cs`
- Create: `src/ObdLink.Core/Dashboard/DashboardWindowPlacement.cs`
- Modify: `src/ObdLink.Core/Safety/AppSettings.cs` (`DashboardOptions` and the finite-bounds check in `Normalize`)
- Test: `tests/ObdLink.Core.Tests/V060Tests.cs`

**Interfaces:**
- Consumes: nothing from Task 2. `DashboardOptions` is the settings object `AppSettings.Dashboard`.
- Produces:
  - `DashboardSession.ShouldRun(bool connected, bool windowOpen, bool keepLiveWhileClosed) -> bool`
  - `DashboardSession.PollAllowed(bool connected, bool windowOpen, bool keepLiveWhileClosed, bool userPaused) -> bool`
  - `DashboardSession.StateText(bool connected, bool windowOpen, bool keepLiveWhileClosed, bool userPaused, bool engaged, bool loopFailed) -> string` returns only `"Stopped"`, `"Paused"`, or `"Live"`
  - `DashboardWindowPlacement.DefaultWidth` = `1100`, `DefaultHeight` = `720`, `MinWidth` = `640`, `MinHeight` = `480`, `MaxWidth` = `4000`, `MaxHeight` = `3000`, `MinVisible` = `64`
  - `DashboardWindowPlacement.MonitorArea(double Left, double Top, double Width, double Height)`
  - `DashboardWindowPlacement.PlacementRequest(bool PlacementSaved, double Left, double Top, double Width, double Height, bool Maximized)`
  - `DashboardWindowPlacement.PlacementResult(double Left, double Top, double Width, double Height, bool Maximize)`
  - `DashboardWindowPlacement.Resolve(PlacementRequest saved, MonitorArea target, IReadOnlyList<MonitorArea> monitors) -> PlacementResult`
  - `DashboardOptions.KeepLiveWhileClosed` (`bool`, default `false`)
  - `DashboardOptions.WindowPlacementSaved` (`bool`, default `false`)
  - `DashboardOptions.WindowLeft`, `WindowTop`, `WindowWidth`, `WindowHeight` (`double`)
  - `DashboardOptions.WindowMaximized` (`bool`, default `false`)

`ShouldRun` is `connected && (windowOpen || keepLiveWhileClosed)`. User pause is not an input. `PollAllowed` is `ShouldRun && !userPaused`.

`StateText` order:

1. `!connected` → `Stopped`
2. `!PollAllowed && (userPaused || engaged)` → `Paused`
3. `!PollAllowed` → `Stopped`
4. `loopFailed` → `Stopped`
5. otherwise → `Live`

`Resolve` rules, in order:

- The saved size is used only when `PlacementSaved` is true and both edges are finite and inside `[MinWidth, MaxWidth]` × `[MinHeight, MaxHeight]`. Otherwise the size is `1100` × `720`, the position is centered in `target`, and `Maximize` is false. Saved `Left` and `Top` are ignored in that case.
- When the size is valid, keep saved `Left` and `Top` only if some monitor overlaps that rectangle by at least 64×64 DIPs. `Maximize` is the saved `Maximized` flag.
- When the size is valid and the overlap test fails, center the saved size in `target` and set `Maximize` false.

- [ ] **Step 1: Write the failing tests**

Append to `V060Tests`:

```csharp
[Theory]
[InlineData(false, true, true, false)]
[InlineData(true, true, false, true)]
[InlineData(true, false, true, true)]
[InlineData(true, false, false, false)]
public void ShouldRun_follows_connection_window_and_keep_live(bool connected, bool windowOpen, bool keepLive, bool expect) =>
    Assert.Equal(expect, DashboardSession.ShouldRun(connected, windowOpen, keepLive));

[Fact]
public void User_pause_does_not_change_ShouldRun_and_blocks_PollAllowed()
{
    Assert.True(DashboardSession.ShouldRun(true, true, false));
    Assert.False(DashboardSession.PollAllowed(true, true, false, userPaused: true));
    Assert.Equal("Paused", DashboardSession.StateText(true, true, false, userPaused: true, engaged: true, loopFailed: false));
    Assert.Equal("Paused", DashboardSession.StateText(true, false, false, userPaused: false, engaged: true, loopFailed: false));
    Assert.Equal("Stopped", DashboardSession.StateText(true, false, false, userPaused: false, engaged: false, loopFailed: false));
    Assert.Equal("Stopped", DashboardSession.StateText(false, true, true, userPaused: true, engaged: true, loopFailed: false));
    Assert.Equal("Live", DashboardSession.StateText(true, false, true, userPaused: false, engaged: false, loopFailed: false));
    Assert.Equal("Stopped", DashboardSession.StateText(true, true, false, userPaused: false, engaged: true, loopFailed: true));
}

[Fact]
public void Placement_centers_unsaved_and_rejects_offscreen_and_bad_sizes()
{
    var screen = new DashboardWindowPlacement.MonitorArea(0, 0, 1920, 1080);
    var fresh = DashboardWindowPlacement.Resolve(new(false, 0, 0, 0, 0, false), screen, new[] { screen });
    Assert.Equal(1100, fresh.Width);
    Assert.Equal(720, fresh.Height);
    Assert.Equal((1920 - 1100) / 2d, fresh.Left);
    Assert.Equal((1080 - 720) / 2d, fresh.Top);
    Assert.False(fresh.Maximize);

    var parked = DashboardWindowPlacement.Resolve(new(true, 100, 80, 900, 600, false), screen, new[] { screen });
    Assert.Equal(100, parked.Left);
    Assert.Equal(80, parked.Top);
    Assert.Equal(900, parked.Width);
    Assert.Equal(600, parked.Height);

    var gone = DashboardWindowPlacement.Resolve(new(true, 5000, 5000, 900, 600, true), screen, new[] { screen });
    Assert.Equal((1920 - 900) / 2d, gone.Left);
    Assert.Equal((1080 - 600) / 2d, gone.Top);
    Assert.Equal(900, gone.Width);
    Assert.Equal(600, gone.Height);
    Assert.False(gone.Maximize);

    var tiny = DashboardWindowPlacement.Resolve(new(true, 10, 10, 100, 100, true), screen, new[] { screen });
    Assert.Equal(1100, tiny.Width);
    Assert.Equal(720, tiny.Height);
    Assert.Equal(fresh.Left, tiny.Left);
    Assert.Equal(fresh.Top, tiny.Top);
    Assert.False(tiny.Maximize);

    var max = DashboardWindowPlacement.Resolve(new(true, 40, 40, 1000, 700, true), screen, new[] { screen });
    Assert.True(max.Maximize);
    Assert.Equal(40, max.Left);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo --filter "FullyQualifiedName~ShouldRun_follows|FullyQualifiedName~User_pause_does_not|FullyQualifiedName~Placement_centers"`

Expected: FAIL. `DashboardSession` does not exist.

- [ ] **Step 3: Implement the two pure types and the settings fields**

Create `src/ObdLink.Core/Dashboard/DashboardSession.cs`:

```csharp
namespace ObdLink.Core.Dashboard;

public static class DashboardSession
{
    public static bool ShouldRun(bool connected, bool windowOpen, bool keepLiveWhileClosed) =>
        connected && (windowOpen || keepLiveWhileClosed);

    public static bool PollAllowed(bool connected, bool windowOpen, bool keepLiveWhileClosed, bool userPaused) =>
        ShouldRun(connected, windowOpen, keepLiveWhileClosed) && !userPaused;

    public static string StateText(bool connected, bool windowOpen, bool keepLiveWhileClosed, bool userPaused, bool engaged, bool loopFailed)
    {
        if (!connected) return "Stopped";
        var allowed = PollAllowed(connected, windowOpen, keepLiveWhileClosed, userPaused);
        if (!allowed && (userPaused || engaged)) return "Paused";
        if (!allowed) return "Stopped";
        if (loopFailed) return "Stopped";
        return "Live";
    }
}
```

Create `src/ObdLink.Core/Dashboard/DashboardWindowPlacement.cs`:

```csharp
namespace ObdLink.Core.Dashboard;

public static class DashboardWindowPlacement
{
    public const double DefaultWidth = 1100;
    public const double DefaultHeight = 720;
    public const double MinWidth = 640;
    public const double MinHeight = 480;
    public const double MaxWidth = 4000;
    public const double MaxHeight = 3000;
    public const double MinVisible = 64;

    public readonly record struct MonitorArea(double Left, double Top, double Width, double Height);
    public readonly record struct PlacementRequest(bool PlacementSaved, double Left, double Top, double Width, double Height, bool Maximized);
    public readonly record struct PlacementResult(double Left, double Top, double Width, double Height, bool Maximize);

    public static PlacementResult Resolve(PlacementRequest saved, MonitorArea target, IReadOnlyList<MonitorArea> monitors)
    {
        var sizeOk = saved.PlacementSaved
            && double.IsFinite(saved.Width) && double.IsFinite(saved.Height)
            && saved.Width >= MinWidth && saved.Width <= MaxWidth
            && saved.Height >= MinHeight && saved.Height <= MaxHeight;
        var width = sizeOk ? saved.Width : DefaultWidth;
        var height = sizeOk ? saved.Height : DefaultHeight;
        var centeredLeft = target.Left + (target.Width - width) / 2;
        var centeredTop = target.Top + (target.Height - height) / 2;
        if (!sizeOk)
            return new(centeredLeft, centeredTop, width, height, false);
        if (!monitors.Any(m => Overlaps(saved.Left, saved.Top, width, height, m)))
            return new(centeredLeft, centeredTop, width, height, false);
        return new(saved.Left, saved.Top, width, height, saved.Maximized);
    }

    private static bool Overlaps(double left, double top, double width, double height, MonitorArea m)
    {
        var x1 = Math.Max(left, m.Left);
        var y1 = Math.Max(top, m.Top);
        var x2 = Math.Min(left + width, m.Left + m.Width);
        var y2 = Math.Min(top + height, m.Top + m.Height);
        return x2 - x1 >= MinVisible && y2 - y1 >= MinVisible;
    }
}
```

Add the seven properties on `DashboardOptions` in `src/ObdLink.Core/Safety/AppSettings.cs` with the defaults in the Interfaces block. In `Normalize`, after the `ArrangeWidth` repair, if any of `WindowLeft`, `WindowTop`, `WindowWidth`, or `WindowHeight` is not finite, set `WindowPlacementSaved` to false. Do not rewrite an out-of-range but finite size here; `Resolve` does that at open time.

- [ ] **Step 4: Run the suite**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ObdLink.Core/Dashboard/DashboardSession.cs src/ObdLink.Core/Dashboard/DashboardWindowPlacement.cs src/ObdLink.Core/Safety/AppSettings.cs tests/ObdLink.Core.Tests/V060Tests.cs
git commit -m "feat: add dashboard poll gating and window placement rules"
```

---

### Task 4: Pop-out window, Modules default, and poll lifecycle

**Files:**
- Create: `src/ObdLink.App/Themes/Dashboard.xaml`
- Create: `src/ObdLink.App/DashboardWindow.xaml`
- Create: `src/ObdLink.App/DashboardWindow.xaml.cs`
- Modify: `src/ObdLink.App/App.xaml`
- Modify: `src/ObdLink.App/MainWindow.xaml`
- Modify: `src/ObdLink.App/MainWindow.xaml.cs`
- Modify: `src/ObdLink.App/MainViewModel.cs:153-156`
- Modify: `src/ObdLink.App/DashboardViewModel.cs`
- Modify: `src/ObdLink.Core/Ui/Shortcuts.cs`
- Modify: `tests/ObdLink.Core.Tests/V040Tests.cs:16` and the `Nav_collapses_to_icons` page asserts
- Modify: `tests/ObdLink.Core.Tests/V033Tests.cs:47-72`
- Test: `tests/ObdLink.Core.Tests/V060Tests.cs`

**Interfaces:**
- Consumes: `DashboardSession.ShouldRun`, `DashboardSession.StateText`, `DashboardOptions.KeepLiveWhileClosed`, `GaugeFaces` (the combo is Task 5; this task only moves the existing surface). `Shortcuts.Resolve` already returns `"page:N"` for Alt+1 through Alt+9.
- Produces:
  - `Shortcuts.Pages` = `{ "Modules", "DTCs", "As-Built", "Vehicle", "Console", "Logs", "Guide", "Settings" }`
  - `Shortcuts.PageByNumber(int oneBased) -> string?` returns the page or null when `oneBased` is outside `1..Pages.Length`
  - Shortcut id `"dashboard"`, gesture `"Ctrl+Shift+G"`, description `"Open the dashboard window"`, group `"View"`
  - `MainViewModel.OpenDashboardCommand` (`RelayCommand`). The command raises `event Action? DashboardOpenRequested`.
  - `MainViewModel.SelectedTab` default `"Modules"`. A set to `"Dashboard"` stores `"Modules"`.
  - `DashboardViewModel.Update(bool windowOpen, bool connected)`
  - `DashboardViewModel.SetWindowOpen(bool open)`
  - `DashboardViewModel.Stop()`
  - `DashboardViewModel.KeepLiveWhileClosed` (`bool` property that writes `DashboardOptions.KeepLiveWhileClosed`, calls `_save()`, and calls `Update`)
  - `DashboardViewModel.WindowOpen` (`bool`, private set)
  - `DashboardViewModel.Engaged` (`bool`, private set, not saved)
  - `MainWindow` holds one `DashboardWindow?`. A second open activates that instance.

Polling rules to implement inside `Update`:

- Remember the last `connected` value.
- `SetWindowOpen(true)` sets `Engaged = true` and calls `Update(true, lastConnected)`.
- `ShouldRun` uses `DashboardSession.ShouldRun(connected, windowOpen, KeepLiveWhileClosed)`.
- When `ShouldRun` becomes true and `_cts` is null, start one loop through the existing `RunAfterAsync` wait. Set `Engaged = true` and clear the loop-failed flag.
- When `ShouldRun` becomes false and `_cts` is not null, cancel and null `_cts`. Do not dispose `_rec`.
- User pause stays `if (Paused) continue` inside the loop.
- On a non-cancel exception, set the loop-failed flag, set `Info` to `"Dashboard stopped: " + ex.Message`, and in the `finally` null `_cts` only when the finishing generation still owns `_cts`. Do not start another loop from that `finally`. The next `Update` that sees `ShouldRun` starts one.
- `StateText` returns `DashboardSession.StateText(...)`.
- `Stop()` sets a stopping flag, cancels and nulls `_cts`, disposes `_rec`, and ignores later `Update` calls. `MainWindow.OnClosing` calls `_dashboard?.Close()` and then `Dashboard.Stop()` so Keep live cannot outlive the process.

- [ ] **Step 1: Write the failing source tests**

Append to `V060Tests`:

```csharp
[Fact]
public void Pages_start_at_modules_and_alt_9_has_no_page()
{
    Assert.Equal(new[] { "Modules", "DTCs", "As-Built", "Vehicle", "Console", "Logs", "Guide", "Settings" }, Shortcuts.Pages);
    Assert.Equal("Modules", Shortcuts.PageByNumber(1));
    Assert.Equal("Settings", Shortcuts.PageByNumber(8));
    Assert.Null(Shortcuts.PageByNumber(9));
    Assert.Null(Shortcuts.PageByNumber(0));
    Assert.Equal("page:9", Shortcuts.Resolve("Alt+9", false));
    Assert.Contains(Shortcuts.All, s => s.Id == "dashboard" && s.Gesture == "Ctrl+Shift+G");
    var vm = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainViewModel.cs"));
    Assert.Contains("_tab = \"Modules\"", vm);
    Assert.Contains("if (value == \"Dashboard\") value = \"Modules\";", vm);
    Assert.Contains("OpenDashboardCommand", vm);
    var xaml = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainWindow.xaml"));
    Assert.DoesNotContain("Header=\"Dashboard\"", xaml);
    Assert.Contains("Header=\"_Dashboard\"", xaml);
    Assert.Contains("OpenDashboardCommand", xaml);
    Assert.Contains("KeepLiveWhileClosed", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "DashboardWindow.xaml")));
    var dash = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "DashboardViewModel.cs"));
    Assert.Contains("DashboardSession.ShouldRun", dash);
    Assert.Contains("DashboardSession.StateText", dash);
    Assert.Contains("_run = RunAfterAsync(_run, _cts.Token)", dash);
    Assert.DoesNotContain("SelectedTab == \"Dashboard\"", vm);
}
```

In `tests/ObdLink.Core.Tests/V040Tests.cs` change `private const int NavTabs = 9;` to `8`. Leave the `Assert.Equal("page:9", Shortcuts.Resolve("Alt+9", false));` line. `Decide` for 8 tabs still matches the existing compact/scroll asserts in that file (labeled stride 60, four-label minimum).

- [ ] **Step 2: Run the new test to verify it fails**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo --filter "FullyQualifiedName~Pages_start_at_modules"`

Expected: FAIL. `PageByNumber` does not exist, and `NavTabs = 8` may already disagree with `Shortcuts.Pages.Length` until Step 3. Fix the compile break in Step 3 before treating a red test as success. The expected failure after Step 3's shortcut edit, if you run again before the XAML edit, is a missing `Header="_Dashboard"` or missing `OpenDashboardCommand`.

- [ ] **Step 3: Shortcuts and the default page**

In `src/ObdLink.Core/Ui/Shortcuts.cs`:

- Change `Pages` to the eight names in the Interfaces block. Update the `"page"` shortcut description to `"Go to page 1-8 (Modules, DTCs, As-Built, Vehicle, Console, Logs, Guide, Settings)"`.
- Add `new("dashboard", "Ctrl+Shift+G", "Open the dashboard window", "View")` next to the other View shortcuts.
- Add `public static string? PageByNumber(int oneBased) { var i = oneBased - 1; return i >= 0 && i < Pages.Length ? Pages[i] : null; }`

In `MainViewModel.SelectedTab`, the field initializer becomes `"Modules"`. At the start of the setter:

```csharp
if (value == "Dashboard") value = "Modules";
```

Replace `SyncDashboard`:

```csharp
private void SyncDashboard() => Dashboard?.Update(Dashboard.WindowOpen, IsConnected && _ch != null);
```

Add the command in the `MainViewModel` constructor next to the other commands:

```csharp
OpenDashboardCommand = new("Open dashboard", EventLog, _ => DashboardOpenRequested?.Invoke());
```

```csharp
public RelayCommand OpenDashboardCommand { get; }
public event Action? DashboardOpenRequested;
```

- [ ] **Step 4: Move the dashboard surface into its own window**

Move the dashboard `DataTemplate`s (from the comment `v0.2.1 layout-driven dashboard` through the `DialCard` template, currently just before `</Window.Resources>` in `MainWindow.xaml`) into `src/ObdLink.App/Themes/Dashboard.xaml` as a `ResourceDictionary`. Merge it from `App.xaml` `Application.Resources` `MergedDictionaries` after `Themes/Controls.xaml`.

Create `DashboardWindow.xaml` with `x:Class="ObdLink.App.DashboardWindow"`, title `Dashboard - OBDLink Diag`, the same icon pack URI as `MainWindow`, `Width="1100"`, `Height="720"`, `MinWidth="640"`, `MinHeight="480"`, `ShowInTaskbar="True"`, `ResizeMode="CanResize"`, `WindowStartupLocation="Manual"`. Do not set `Window.Owner`.

The body is the current Dashboard `TabItem` content: the `DockPanel` whose `DataContext` is `{Binding Dashboard}`. Delete the `TabItem` from `MainWindow.xaml`. The first remaining `TabItem` is Modules.

Add a checkable **Keep live** toggle on that toolbar, bound to `KeepLiveWhileClosed`, tooltip `Keep polling while this window is closed`.

Move these methods and their fields from `MainWindow.xaml.cs` into `DashboardWindow.xaml.cs`, retargeting `_vm.Dashboard` to the window's dashboard (`((MainViewModel)DataContext).Dashboard`): `ApplyDashFlow`, `ApplyDashBackground`, `DashCanvas_SizeChanged`, `DashCanvas_ScrollChanged`, `DashItem_MouseDown`, `DashItem_MouseMove`, `DragSourceKey`, `DashItem_Drop`, `DashItem_DragOver`, `DashHost_DragOver`, `DashHost_Drop`, `DashRowSize`, `DashMove`, `DashItemRow_Click`, `DashItem_KeyDown`, `SlotCenters`, `SetDashDropTargets`. Move the field group `_dragStart`, `_dragItem`, `_dragKey`, `_dropSrc`, `_dropDst` with them.

`DashboardWindow` constructor subscribes to `Dashboard.PropertyChanged` for `BackgroundId`, `BackgroundFile`, and `BackgroundFullPath`, and calls `ApplyDashBackground` on load, the same three lines that `MainWindow` uses today. Remove that subscription and the `ApplyDashFlow()` call inside `MainWindow.SetZoom` from `MainWindow.xaml.cs`.

`DashboardWindow.Loaded` calls the view-model method, it does not construct a view model:

```csharp
private DashboardViewModel Dash => ((MainViewModel)DataContext).Dashboard;
private void OnLoaded(object sender, RoutedEventArgs e) => Dash.SetWindowOpen(true);
```

`OnClosing` is where Task 6 will save placement. In this task the override only calls `base.OnClosing(e)`. `Closed` is handled by `MainWindow`, not here.

- [ ] **Step 5: One window, and the poll loop follows it**

In `MainWindow.xaml.cs`:

```csharp
private DashboardWindow? _dashboard;
private WindowState _dashboardRestore = WindowState.Normal;

private void OpenDashboard()
{
    if (_dashboard != null)
    {
        if (_dashboard.WindowState == WindowState.Minimized)
            _dashboard.WindowState = _dashboardRestore;
        _dashboard.Activate();
        return;
    }
    _dashboard = new DashboardWindow { DataContext = _vm };
    _dashboard.StateChanged += (_, _) =>
    {
        if (_dashboard is { WindowState: WindowState.Normal or WindowState.Maximized } w)
            _dashboardRestore = w.WindowState;
    };
    _dashboard.Closed += (_, _) => { _dashboard = null; _vm.Dashboard.SetWindowOpen(false); };
    _dashboard.Show();
}

protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
{
    _dashboard?.Close();
    _vm.Dashboard.Stop();
    base.OnClosing(e);
}
```

Subscribe in the `MainWindow` constructor: `_vm.DashboardOpenRequested += OpenDashboard;`

`RunShortcut` page case becomes:

```csharp
case var p when p.StartsWith("page:"):
    if (ObdLink.Core.Ui.Shortcuts.PageByNumber(p[5] - '0') is { } page) Go(page);
    break;
```

`p[5] - '0'` is the one-based digit. Add `case "dashboard": Run(_vm.OpenDashboardCommand); break;`

In `MainWindow.xaml` View menu, insert as the first item:

```xml
<MenuItem Header="_Dashboard" InputGestureText="Ctrl+Shift+G" Command="{Binding OpenDashboardCommand}"/>
```

Remove the Tools menu item whose `Tag` is `Dashboard` (`Header="_Dashboard"` under `_Tools`, the one with `Click="Nav_Click"`).

In the header `WrapPanel`, immediately after the `TextBlock` whose text is `OBDLink Diag`, add:

```xml
<Button Command="{Binding OpenDashboardCommand}" ToolTip="Open the dashboard window (Ctrl+Shift+G). If it is already open, it is brought to the front." Margin="0,0,8,0">
  <StackPanel Orientation="Horizontal"><TextBlock Style="{StaticResource GlyphStyle}" Text="&#xEC4A;"/><TextBlock Text="Dashboard"/></StackPanel>
</Button>
```

On the Settings page, replace the Background combo tooltip with `Material photo, None for the app theme, or a custom image. Saved in Data\settings.json. A custom image is copied into Data\backgrounds.` Replace the Customize hint text with `Dashboard items: use Customize on the dashboard window (add / remove / reorder / display type / Analog or Digital / saved layouts). Gauge-height cards stack only when the dashboard window is narrower than a saved card.`

In `DashboardViewModel`:

- Change the `_info` initializer to `Connect (a DEMO port works), then open the dashboard window.`
- Replace `Update(bool pageVisible, bool connected)` with the rules in this task's Interfaces. Keep the method name `Update`.
- Add `SetWindowOpen`, `Stop`, `WindowOpen`, `Engaged`, `KeepLiveWhileClosed`, and a `_loopFailed` flag.
- `StateText` calls `DashboardSession.StateText(_connected, WindowOpen, KeepLiveWhileClosed, Paused, Engaged, _loopFailed)`.
- The `Paused` setter already raises `StateText`. Keep that.
- Closing the window must not dispose `_rec`. `Stop()` does.

Update `tests/ObdLink.Core.Tests/V033Tests.cs` `ViewHasKeyboardButtonsSizePickerAndDrag` so the XAML strings (`Move left`, `DropOverlay`, `FlowWidth`, `DashItem_Drop`, and the rest of that list) are read from `Themes/Dashboard.xaml` and `DashboardWindow.xaml` concatenated, and the code-behind strings (`Key.Left => -1`, `SetDashDropTargets(true)`, `_dragKey`, `DataFormats.UnicodeText`) are read from `DashboardWindow.xaml.cs`. `MoveBy` and `DashReorder` stay asserted against `DashboardViewModel.cs`.

- [ ] **Step 6: Run the suite**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/ObdLink.App src/ObdLink.Core/Ui/Shortcuts.cs tests/ObdLink.Core.Tests/V060Tests.cs tests/ObdLink.Core.Tests/V040Tests.cs tests/ObdLink.Core.Tests/V033Tests.cs
git commit -m "feat: open the dashboard in a single pop-out window"
```

---

### Task 5: Draw Digital dials and add the face combo

**Files:**
- Modify: `src/ObdLink.App/Controls/Gauges.cs` (`CircularGauge`)
- Modify: `src/ObdLink.App/DashboardViewModel.cs` (`GaugeVm`, `DashItemVm`)
- Modify: `src/ObdLink.App/Themes/Dashboard.xaml` (`DialBody` and the Customize row of `DashItemCard`)
- Test: `tests/ObdLink.Core.Tests/V060Tests.cs`

**Interfaces:**
- Consumes: `GaugeFaces.ShowsNeedle(DisplayKind, string?)`, `GaugeFaces.Analog`, `GaugeFaces.Digital`, `DashItem.Face`.
- Produces:
  - `GaugeVm.ShowAnalog` (`bool`, default `true`)
  - `DashItemVm.Faces` = `{ GaugeFaces.Analog, GaugeFaces.Digital }`
  - `DashItemVm.ShowFaceChoice` (`bool`) true when `Entry.Displays` contains `DisplayKind.Dial`
  - `DashItemVm.Face` (`string`) writes `Item.Face` through `GaugeFaces.Normalize` and calls `Changed`
  - `DashItemVm.SyncFace()` sets `Gauge.ShowAnalog = GaugeFaces.ShowsNeedle(Item.Display, Item.Face)`
  - `CircularGauge.ShowAnalog` dependency property, default `true`, `AffectsRender`

- [ ] **Step 1: Write the failing source test**

Append to `V060Tests`:

```csharp
[Fact]
public void Digital_dials_hide_the_needle_and_text_only_cards_have_no_face_combo()
{
    var gauges = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "Controls", "Gauges.cs"));
    Assert.Contains("ShowAnalogProperty", gauges);
    Assert.Contains("if (ShowAnalog)", gauges);
    var xaml = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "Themes", "Dashboard.xaml"));
    Assert.Contains("ShowAnalog=\"{Binding ShowAnalog}\"", xaml);
    Assert.Contains("SelectedItem=\"{Binding Face}\"", xaml);
    Assert.Contains("ShowFaceChoice", xaml);
    var vm = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "DashboardViewModel.cs"));
    Assert.Contains("GaugeFaces.ShowsNeedle", vm);
    Assert.Contains("SyncFace()", vm);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo --filter "FullyQualifiedName~Digital_dials_hide_the_needle"`

Expected: FAIL. `ShowAnalogProperty` is absent.

- [ ] **Step 3: Gate the analog drawing**

On `CircularGauge`, add:

```csharp
public static readonly DependencyProperty ShowAnalogProperty = DependencyProperty.Register(
    nameof(ShowAnalog), typeof(bool), typeof(CircularGauge), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
public bool ShowAnalog { get => (bool)GetValue(ShowAnalogProperty); set => SetValue(ShowAnalogProperty, value); }
```

In `OnRender`, wrap the track, warning arc, value arc, ticks, needle, and hub in `if (ShowAnalog)`. Always draw the title, value, and unit. When `ShowAnalog` is false, draw the value at the center with `size * 0.22` and the unit directly under it, and draw the title above the value. When `ShowAnalog` is true, keep today's positions (`size * 0.15` value below center, title at `c.Y - r * 0.45`).

On `GaugeVm` add `public bool ShowAnalog { get; set; } = true;` and include `nameof(ShowAnalog)` in `Refresh`.

On `DashItemVm` add `Faces`, `ShowFaceChoice`, `Face`, and `SyncFace` as specified. Call `SyncFace()` at the end of the constructor, from the `Display` setter, and from the `Face` setter. `BuildGauges` already constructs `DashItemVm`, so the constructor call covers layout reloads.

In `Themes/Dashboard.xaml` `DialBody`, add `ShowAnalog="{Binding ShowAnalog}"` on `CircularGauge`. In the `DashItemCard` Customize `WrapPanel`, after the display `ComboBox`, add:

```xml
<ComboBox MinWidth="80" ItemsSource="{Binding Faces}" SelectedItem="{Binding Face}" Visibility="{Binding ShowFaceChoice, Converter={StaticResource BoolVis}}" ToolTip="Analog draws the needle and arc when Display is Dial. Digital draws digits only. Bar, tile, and lamp stay as they are."/>
```

Add `<BooleanToVisibilityConverter x:Key="BoolVis"/>` to `Themes/Dashboard.xaml`. Leave the existing `BoolVis` resource in `MainWindow.xaml`; the main window still uses it. The dashboard templates look up `BoolVis` from application resources once they live in `Themes/Dashboard.xaml`, so the key has to be in that dictionary.

- [ ] **Step 4: Run the suite**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo`

Expected: PASS. `ShowsNeedle` is already covered in Task 2, and `DashboardFlow.Metrics` does not take `Face`, so card sizes stay put when the face flips.

- [ ] **Step 5: Commit**

```bash
git add src/ObdLink.App/Controls/Gauges.cs src/ObdLink.App/DashboardViewModel.cs src/ObdLink.App/Themes/Dashboard.xaml src/ObdLink.App/MainWindow.xaml tests/ObdLink.Core.Tests/V060Tests.cs
git commit -m "feat: toggle analog and digital faces on needle gauges"
```

---

### Task 6: Save and restore dashboard window bounds

**Files:**
- Create: `src/ObdLink.App/MonitorAreas.cs`
- Modify: `src/ObdLink.App/DashboardWindow.xaml.cs`
- Test: `tests/ObdLink.Core.Tests/V060Tests.cs`

**Interfaces:**
- Consumes: `DashboardWindowPlacement.Resolve`, `PlacementRequest`, `PlacementResult`, `MonitorArea`, and the `DashboardOptions` fields from Task 3 (`WindowPlacementSaved`, `WindowLeft`, `WindowTop`, `WindowWidth`, `WindowHeight`, `WindowMaximized`). `DashboardViewModel` already has `_settings()` and `_save()`.
- Produces:
  - `MonitorAreas.WorkingAreasInDips(Visual visual) -> IReadOnlyList<DashboardWindowPlacement.MonitorArea>`
  - `DashboardViewModel.CapturePlacement(double left, double top, double width, double height, bool maximized)` writes the five fields, sets `WindowPlacementSaved` true, and calls `_save()`
  - `DashboardViewModel.SavedPlacement -> DashboardWindowPlacement.PlacementRequest`

- [ ] **Step 1: Write the failing source test**

Append to `V060Tests`:

```csharp
[Fact]
public void Dashboard_window_saves_dips_through_the_placement_resolver()
{
    var win = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "DashboardWindow.xaml.cs"));
    Assert.Contains("DashboardWindowPlacement.Resolve", win);
    Assert.Contains("CapturePlacement", win);
    Assert.Contains("RestoreBounds", win);
    Assert.DoesNotContain("Window.Owner", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "DashboardWindow.xaml")));
    Assert.Contains("GetDpiForMonitor", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MonitorAreas.cs")));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo --filter "FullyQualifiedName~Dashboard_window_saves_dips"`

Expected: FAIL. `MonitorAreas.cs` does not exist.

- [ ] **Step 3: Read monitors in DIPs and apply Resolve**

`MonitorAreas.WorkingAreasInDips` enumerates monitors with `EnumDisplayMonitors` and `GetMonitorInfo`. Convert each `rcWork` to DIPs with `GetDpiForMonitor` (`MDT_EFFECTIVE_DPI` = 0, `shcore.dll`): divide pixel edges by `dpi / 96`. If the list is empty, return one area from `SystemParameters.WorkArea`. Mark the primary monitor (`MONITORINFOF_PRIMARY` = 1) by returning it as the first element when the caller asks for a fallback.

`DashboardViewModel.SavedPlacement` returns `new DashboardWindowPlacement.PlacementRequest(d.WindowPlacementSaved, d.WindowLeft, d.WindowTop, d.WindowWidth, d.WindowHeight, d.WindowMaximized)`.

`CapturePlacement` assigns those fields from its arguments, sets `WindowPlacementSaved = true`, and calls `_save()`.

In `DashboardWindow`, on `SourceInitialized`:

- Build the monitor list.
- The target monitor is the one that contains the main window center `(main.Left + main.Width / 2, main.Top + main.Height / 2)`. If none contains it, use the primary (first) area.
- `var place = DashboardWindowPlacement.Resolve(vm.SavedPlacement, target, monitors);`
- Set `WindowStartupLocation = Manual`, then `Left`, `Top`, `Width`, `Height`.
- If `place.Maximize`, set `WindowState = Maximized` after the Normal bounds are applied.

On `OnClosing`, if `WindowState == Maximized`, read `RestoreBounds` for the Normal rectangle; otherwise read `Left`, `Top`, `Width`, `Height`. Call `CapturePlacement(left, top, width, height, WindowState == Maximized)`.

Do not write a placement file outside `settings.json`. `CapturePlacement` uses the existing `_save()` path, which writes `AppPaths.SettingsFile` under `Data\`.

- [ ] **Step 4: Run the suite**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo`

Expected: PASS. The numeric cases for `Resolve` already passed in Task 3.

- [ ] **Step 5: Commit**

```bash
git add src/ObdLink.App/MonitorAreas.cs src/ObdLink.App/DashboardWindow.xaml.cs src/ObdLink.App/DashboardViewModel.cs tests/ObdLink.Core.Tests/V060Tests.cs
git commit -m "feat: remember the dashboard window size and position"
```

---

### Task 7: Guide and in-app shortcut copy

**Files:**
- Modify: `docs/GUIDE.md` (Dashboard section around the "Open Dashboard on the left rail" paragraph, the background bullet, the left-rail Alt+1 bullet, and the shortcut table row for Alt+1..9)
- Modify: `tests/ObdLink.Core.Tests/V052Tests.cs` method `Version_is_0_5_2`
- Test: `tests/ObdLink.Core.Tests/V060Tests.cs`

**Interfaces:**
- Consumes: the user-visible names from Tasks 1 and 4 (`Fine wood grain`, `Brushed aluminum`, `High-gloss black`, `Diamond-plate metal`, `Carbon fiber`, `Sports-car leather`, Keep live, Ctrl+Shift+G). `Shortcuts.Markdown()` and `Shortcuts.Text()` read `Shortcuts.All`, so the new shortcut appears in Help once Task 4 added it.
- Produces: guide prose that matches the shipped UI. Release notes stay headed `## v0.5.2` until Task 8. `<Version>` stays `0.5.2` until Task 8.

- [ ] **Step 1: Write the failing doc test**

Append to `V060Tests`:

```csharp
[Fact]
public void Guide_describes_the_popout_and_the_materials()
{
    var guide = File.ReadAllText(Path.Combine(Root(), "docs", "GUIDE.md"));
    Assert.Contains("dashboard window", guide, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("Keep live", guide);
    Assert.Contains("Fine wood grain", guide);
    Assert.Contains("Brushed aluminum", guide);
    Assert.Contains("High-gloss black", guide);
    Assert.Contains("Diamond-plate metal", guide);
    Assert.Contains("Carbon fiber", guide);
    Assert.Contains("Sports-car leather", guide);
    Assert.Contains("Ctrl+Shift+G", guide);
    Assert.DoesNotContain("Analog gauges", guide);
    Assert.DoesNotContain("Digital dash", guide);
    Assert.Contains("UniformToFill", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "DashboardBackdrop.cs")));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo --filter "FullyQualifiedName~Guide_describes_the_popout"`

Expected: FAIL. The guide still says to open Dashboard on the left rail and still lists Analog gauges and Digital dash.

- [ ] **Step 3: Rewrite the dashboard section of the guide**

In `docs/GUIDE.md`:

- The Dashboard section opens by telling the reader to use View → Dashboard, the header Dashboard button, or Ctrl+Shift+G. It states there is one window, and a second invocation brings that window to the front.
- Pause still stops polling. Closing the window also stops polling. **Keep live** keeps polling while the window is closed and is remembered in `Data\settings.json`. Reopening resumes polling that stopped because the window closed. A Pause the user turned on stays on until they resume.
- Customize, drag, Move buttons, sizes, layouts, and narrow-window stacking stay, and they apply to the dashboard window rather than the main window.
- Replace the v0.5.2 background bullet with the six material names, None, and Custom. State that each built-in photo covers the window once (`UniformToFill`, not tiled), that a saved cabin choice from v0.5.2 opens as None, and that Pick image still copies into `Data\backgrounds`.
- Alt+1 opens Modules. Alt+2 through Alt+8 open the remaining pages. The shortcut table row becomes `Alt+1..8` with the page list Modules, DTCs, As-Built, Vehicle, Console, Logs, Guide, Settings. Add a row `Ctrl+Shift+G` | Open the dashboard window.
- Use "tire" and "aluminum". Do not describe the pictures as a Ford cabin.

`Version_is_0_5_2` asserts the guide contains `Analog gauges` and `Digital dash`. Remove those two `Assert.Contains` lines from that method in this task. Leave the asserts that `<Version>` is `0.5.2` and that release notes start with `## v0.5.2`.

- [ ] **Step 4: Run the suite**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add docs/GUIDE.md tests/ObdLink.Core.Tests/V052Tests.cs tests/ObdLink.Core.Tests/V060Tests.cs
git commit -m "docs: describe the dashboard pop-out and material backgrounds"
```

---

### Task 8: Version 0.6.0, open-PR scan, and portable zips

**Files:**
- Modify: `src/ObdLink.App/ObdLink.App.csproj` (`<Version>`)
- Modify: `src/ObdLink.Core/ObdLink.Core.csproj` (`<Version>`)
- Modify: `docs/RELEASE-NOTES.md` (prepend a `## v0.6.0` section; leave every older section in place)
- Modify: `README.md` (the Windows row in the version table, today `v0.5.2`)
- Modify: `tests/ObdLink.Core.Tests/V052Tests.cs` method `Version_is_0_5_2`
- Modify: `.gitignore` (add an `artifacts/` line)
- Test: `tests/ObdLink.Core.Tests/V060Tests.cs`

**Interfaces:**
- Consumes: the finished feature from Tasks 1–7. Zip names from the spec: `ObdLinkApp-win-x64-v0.6.0.zip` and `ObdLinkApp-ARM64-only-v0.6.0.zip`.
- Produces: both csproj `<Version>` elements equal `0.6.0`. Release notes start with `## v0.6.0`. No installer project. No RID other than `win-x64` and `win-arm64`.

- [ ] **Step 1: Write the failing version test**

Rename `Version_is_0_5_2` to `Version_is_0_6_0` and change it so it asserts:

- both csproj files contain `<Version>0.6.0</Version>`
- `docs/RELEASE-NOTES.md` starts with `## v0.6.0` and still contains `## v0.5.2`
- the v0.6.0 section contains `ObdLinkApp-win-x64-v0.6.0.zip`, `ObdLinkApp-ARM64-only-v0.6.0.zip`, `Keep live`, `Fine wood grain`, `not tiled`, `generated`, and `MIT`
- the v0.6.0 section says the ARM64 zip was built and was not run on ARM64 hardware, unless you actually ran the spec's Windows checks on ARM64, in which case say that those checks passed
- README contains `v0.6.0`
- the notes do not contain `v0.6.0-test`

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo --filter "FullyQualifiedName~Version_is_0_6_0"`

Expected: FAIL. The csproj version is still `0.5.2`.

- [ ] **Step 3: Scan open pull requests before the version bump is merged**

Run:

```bash
gh pr list --state open --base master --json number,title,headRefName,url
```

For each open pull request other than the v0.6.0 feature pull request, run `gh pr diff NUMBER --name-only`. If that diff touches any file in the File Structure table, rebase that branch onto the feature branch and push it before merge, or rebase it onto `master` immediately after the feature merges. If an open pull request is a bugfix that would be absent from `master` after this merge, do not tag `v0.6.0` until that pull request is merged or rebased onto the release commit. Record the scan result in the pull request conversation (numbers reviewed, and which ones were rebased).

- [ ] **Step 4: Bump the version and write the release notes**

Set `<Version>0.6.0</Version>` in both csproj files. Prepend `docs/RELEASE-NOTES.md` with a `## v0.6.0` section that covers the pop-out, Keep live, the six materials, the cabin-id fallback to None, the Analog/Digital face, the two zip names, portable/no installer, and the Windows checks copied from the spec's "Windows pass" list. Update the README Windows table row from `v0.5.2` to `v0.6.0` and point the verify note at the v0.6.0 release notes.

Add this line to `.gitignore`:

```text
artifacts/
```

- [ ] **Step 5: Publish the two zips**

```bash
dotnet publish src/ObdLink.App/ObdLink.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
dotnet publish src/ObdLink.App/ObdLink.App.csproj -c Release -r win-arm64 --self-contained true -o artifacts/publish/win-arm64
(cd artifacts/publish/win-x64 && zip -r ../../ObdLinkApp-win-x64-v0.6.0.zip .)
(cd artifacts/publish/win-arm64 && zip -r ../../ObdLinkApp-ARM64-only-v0.6.0.zip .)
```

Expected: both zips exist under `artifacts/` and each contains `OBDLinkDiag.exe`. `git status` does not show `artifacts/` because of the gitignore line. Do not commit the zips or the publish folders. Attach the zips to the GitHub release when the tag is pushed. Do not create an installer.

- [ ] **Step 6: Run the suite, then the Windows pass**

Run: `dotnet test tests/ObdLink.Core.Tests/ObdLink.Core.Tests.csproj --nologo`

Expected: PASS.

On a Windows 10 version 1607 or later machine, or on Windows 11, unzip `ObdLinkApp-win-x64-v0.6.0.zip` and run the ten checks in the spec under "Windows pass". The ARM64 zip must already have been built in Step 5. Run those same ten checks on ARM64 Windows before the release notes call that zip Windows-tested. When that hardware is not used, the release notes sentence from Step 1 stands.

- [ ] **Step 7: Commit**

```bash
git add src/ObdLink.App/ObdLink.App.csproj src/ObdLink.Core/ObdLink.Core.csproj docs/RELEASE-NOTES.md README.md tests/ObdLink.Core.Tests/V052Tests.cs .gitignore
git commit -m "chore: release v0.6.0 portable zips"
```

Do not tag until Step 3's pull-request scan is done.
