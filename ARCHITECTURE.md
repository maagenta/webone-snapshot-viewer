# Architecture: `--web-snapshot-viewer`

## General flow

```
Browser → Proxy → [has dimensions?]
                    ├─ NO → Return "probe page" (minimal HTML with JS)
                    │        Browser runs JS → redirects with dimensions → Proxy
                    └─ YES → Playwright screenshot → HTML with <img 100%x100%>
```

## File structure

```
./web-snapshot-viewer/
├── WebSnapshotViewer.cs      # Entry point, orchestrates the flow
├── DimensionProbe.cs         # Generates lightweight HTML to detect w/h
├── ScreenshotEngine.cs       # Playwright wrapper (screenshot → JPG base64)
└── SnapshotPage.cs           # Generates final HTML with <img>
```

---

## Interception point in the proxy

The ideal place is in `HttpTransit.cs` (or wherever the response decision is made). The flow:

```
HttpRequestProcessor → HttpTransit → [SnapshotMode?]
                                        ├─ YES → WebSnapshotViewer.Handle(request)
                                        └─ NO  → normal flow
```

In `Program.cs`:
```csharp
public static bool SnapshotViewerMode = false;
```

In `ProcessCommandLine`:
```csharp
case "--web-snapshot-viewer":
    SnapshotViewerMode = true;
    break;
```

---

## Detailed 2-step flow

### Step 1 — Probe page (first time browser requests a URL)

Proxy has no dimensions yet → returns:

```html
<!DOCTYPE html><html><body><script>
var t="http://snapshot.webone.internal/snap";
var u=encodeURIComponent(location.href);
location.replace(t+"?url="+u+"&w="+window.innerWidth+"&h="+window.innerHeight);
</script></body></html>
```

Very lightweight, no CSS, no external resources. Browser runs JS and redirects to the proxy with its dimensions.

### Step 2 — Screenshot (when request arrives with `w` and `h`)

Proxy intercepts `http://snapshot.webone.internal/snap?url=...&w=...&h=...` → calls `ScreenshotEngine` → Playwright opens the URL with viewport `w x h` → full-page JPG → responds:

```html
<!DOCTYPE html><html>
<body style="margin:0;padding:0;overflow-x:hidden">
<img src="data:image/jpeg;base64,..." style="width:100%;display:block">
</body></html>
```

---

## Key design decisions

| Decision | Recommended option | Reason |
|---|---|---|
| How to pass dimensions | JS redirect to magic hostname | No cookies, no proxy-side state |
| Magic hostname | `snapshot.webone.internal` | Easy to intercept before SSL |
| Image format | Inline base64 JPG | No temp files, no second request |
| Playwright via | `Microsoft.Playwright` NuGet | Official C# support |
| Normal flow bypass | Global `SnapshotViewerMode` flag in `Program.cs` | Other processors check it before acting |
| Screenshot scope | Full-page (`FullPage = true`) | Captures entire vertical content, not just viewport |
| Cache key | `SHA256(url + width)` | Height irrelevant for full-page; width affects layout |
| Cache TTL | 5 minutes (configurable) | Balance between freshness and performance |

---

## Screenshot full-page

Playwright has native support:
```csharp
await page.ScreenshotAsync(new() {
    FullPage = true,
    Type = ScreenshotType.Jpeg,
    Quality = 85
});
```
The browser `width` sets the viewport, `height` does not limit the capture — Playwright captures all vertical content.

## Cache structure in `WebSnapshotViewer.cs`

```csharp
private static readonly Dictionary<string, (byte[] jpg, DateTime taken)> Cache = new();
private static readonly TimeSpan CacheTTL = TimeSpan.FromMinutes(5);
```

Before calling Playwright:
```
Does Cache[key] exist and is younger than TTL? → return cached JPG
Otherwise → take screenshot → store in cache → return
```

## Exclusive mode

All requests go through `WebSnapshotViewer.Handle()`. The proxy ignores all `ConfigFile` rewrite rules but keeps SSL spoofing active (needed to intercept HTTPS before redirecting to the viewer).

---

## Dependency to add in `.csproj`

```xml
<PackageReference Include="Microsoft.Playwright" Version="1.*" />
```

---

## Full diagram

```
HTTPS request → SSL spoof → WebSnapshotViewer.Handle()
HTTP  request →             WebSnapshotViewer.Handle()
                                     │
                        Is URL snapshot.webone.internal?
                               │                │
                              NO               YES
                               │                │
                         Probe page         Cache hit?
                         (JS redirect)       │       │
                                            YES      NO
                                             │       │
                                         Cached   Playwright
                                          JPG    (viewport=w,
                                                  full-page)
                                                     │
                                               Store in cache
                                                     │
                                            HTML <img 100%>
```
