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

Three views, and three points to drill from: **Configuration**, the classic
tree, holding still; **Monitor**, the board that follows Receive → Process →
Send; and **Topology** (ADR-0052, amendments 2026-09-14 and 2026-09-18). A
scope reached in one leads to the same scope in the others, written in one
place, `ScopeLink`: a Configuration row to the Monitor's drill, the Monitor's
drill to the Configuration row, a node or a link selected on the Topology to
both. In the tree a branch that is not fine names the leaf that explains it
and links to that leaf's row, with the way to it standing open. The Topology
is always what is configured and what is observed, together; there is no
switch between them.

`src/Xmip.Gui.Test` holds the pages' tests (ADR-0052 clause 6): bUnit renders
the three views over the surface library's own published fixture, so what is
asserted is what an operator sees and no surface is faked.

The cluster board follows Xmip's
Receive → Process → Send path. The communication topology aggregates configured
and observed relationships between infrastructure endpoints, then drills through
services, processes, interfaces, ports, protocols and locations. Its animated
movement represents application semantics — request/response, send/receive,
publish/consume, streaming, fire-and-forget, sessions and retries — rather than
decorative packets or an assumed bidirectional flow. The snapshot surface reads
this model, and the Playground's fleet publishes it since 2026-09-14: the fleet,
the shared store, one process per named node and each node's exchanges over
claim, daily and its own snapshot (ADR-0052, amendment 2026-09-14, ruling 3).
The native operator boundary does not publish topology yet, so over that
surface the view says so plainly; a node run from configuration is queue item
1. Xmip draws what is configured and what is observed and infers nothing from
a socket. Both views subscribe to the shared
operator change stream rather than running independent refresh timers. Blazor
carries resulting renders to the web client through its existing SignalR
circuit; the desktop uses the same Razor components and notifications.

Monitoring is not yet a designer; it shows what Xmip moves and which systems participate. What the market makes of that is `doc/planning/market-position.md` in the estate.

The look is the logo's own green, the lime, `#75c93b` (the owner, 2026-09-12).
It is the accent for whatever is chosen, active or primary: the
view in the top bar, a selected stage, a hovered row, a primary button, the
lines and the movement in the topology. It is never a mood; the moods keep the
colors ADR-0041 maps them to, and Fine is the same family one cut deeper so the
word stays legible on white. `xmip.css` holds the tokens, `--brand` and its
deep and tinted cuts, and nothing else names a color.

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
`[Xmip]`: `Surface = "native" | "snapshot" | "remote"`, `RuntimeLibrary`
(else `XMIP_RUNTIME_LIBRARY`, else beside the executable), `Snapshot = <path>`
when the surface is a snapshot — a path with no file behind it is said so on
the page — `Url = <web host>` when the surface is remote, and, on the desktop
only, `NodeConfiguration` and `Role`. The web host serves its own surface at
`/surface`: a SignalR hub every remote surface follows and is told through
when this host's surface changes, so the CLI, the PowerShell module and a GUI
on another machine follow it without polling (ADR-0052, amendment
2026-09-15). TOML,
because Xmip configures nothing in JSON anywhere — the web host's default
`appsettings.json` sources are removed in `Program.cs` and the TOML reader in
`Xmip.Surface` takes their place.
