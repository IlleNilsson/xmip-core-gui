# xmip-core-gui

The graphical operator surface: a Blazor component library over the Xmip ABI,
with two hosts — .NET 11 MAUI on the desktop and .NET 11 Blazor on the web.

One component library, two hosts, zero drift: both render the same components
calling the same ABI, per ADR-0014. What the desktop shows and what the web
shows cannot disagree, because there is only one implementation to disagree
with.

## State

The web host exists: `src/Xmip.Gui.Web`, a .NET 11 Blazor Web App with one
screen over the operator boundary in `xmip_operate.h`. **The landing page is
the cluster and its message path** — Receive, Process, Send, each with its
health and what it moved — because that is what an operator runs. Nodes are
infrastructure and sit at the bottom, for when a stage is red and the question
becomes where. Stated by the owner, 2026-09-05.
It loads `xmip_core_runtime` and reads its table; when it cannot, a stand-in
answers and every page says SAMPLE in red. Started 2026-09-04.

The market survey calls this gap wider than any open runtime feature — every
competitor leads with a visual designer. One screen is not a designer. It is
the first thing an operator can look at.

Run it: `dotnet run --project src/Xmip.Gui.Web`, then open http://localhost:5087.

Configuration is `src/Xmip.Gui.Web/xmip.gui.toml`: where the runtime library
is, which node to start, the port, logging. TOML, because Xmip configures
nothing in JSON anywhere — the host's default `appsettings.json` sources are
removed in `Program.cs` and a TOML provider takes their place.
