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
Started 2026-09-04.

**The screens are all this repository holds** (ADR-0052). The surface they
read, the scope tree and its rollup, the worst leaf beneath a scope, runtime
discovery and the English live in `Xmip.Surface` beside the binding in
xmip-core-abi (`dotnet/Xmip.Surface`), shared with the cli and the PowerShell
module; `Xmip.Gui` keeps the Razor and the role. There is no sample surface: a
host reads the runtime's own table (`NativeOperator` over `Xmip.Abi`) or a
snapshot a node published (`SnapshotOperator`), and which one is stated in its
`xmip.gui.toml`, never guessed. A scope that is Holding says why beside the
word — the worst leaf beneath it and that leaf's evidence — at the banner, the
stage tile, the node row and every branch of the drill-down; there is no
*follow the error* button, the operator drills or reads the audit.

The market survey calls this gap wider than any open runtime feature — every
competitor leads with a visual designer. One screen is not a designer. It is
the first thing an operator can look at.

Two hosts, one library. ADR-0014.

**The configuration tool has two faces** (ADR-0014, amendment 2026-09-10):
operators run the MAUI executable; developers edit the same node configuration
in VS Code. The extension is the nested technology repository
`vscode/` (xmip-core-gui-vscode): a Rust language server, `xmip-lsp`, over the
same `xmip_validate_v1`, with a TypeScript shell — the one place in the estate
that has any.

**Web** — `dotnet run --project src/Xmip.Gui.Web`, then open http://localhost:5087.
The web GUI monitors and does nothing else (ADR-0014, amendment 2026-09-05):
it starts no node, offers no Pause or Resume, and its role is Observer, not
configurable.

**Desktop** — `dotnet run --project src/Xmip.Operations -f net11.0-windows10.0.19041.0`.
A native window; needs the maui-windows workload; Windows is the only
platform it carries. Same screen, same operator boundary, so the two cannot
disagree — and the desktop configures: it starts the node its configuration
names, and its Configure page validates and starts through the runtime.

Configuration is each host's `xmip.gui.toml`, with the same keys under
`[Xmip]`: `Surface = "native" | "snapshot"`, `RuntimeLibrary` (else
`XMIP_RUNTIME_LIBRARY`, else beside the executable), `Snapshot = <path>` when
the surface is a snapshot — a path with no file behind it is said so on the
page — and, on the desktop only, `NodeConfiguration` and `Role`. TOML,
because Xmip configures nothing in JSON anywhere — the web host's default
`appsettings.json` sources are removed in `Program.cs` and the TOML reader in
`Xmip.Surface` takes their place.
