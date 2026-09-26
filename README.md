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
place, `ScopeLink`: a Configuration row to the Monitor's drill and to the
same thing on the Topology, the Monitor's drill to the Configuration row, a
node on the Topology to its drill and, beside the canvas, to both. The tree's
first row is the cluster and nothing heads it twice; a row's kind is what the
publisher's topology says the thing is — cluster, node, stage, endpoint —
where one is published. In the tree a branch that is not fine names the leaf
that explains it and links to that leaf's row, with the way to it standing
open. The Topology is always what is configured and what is observed,
together; there is no switch between them. It opens on the cluster's nodes and
the traffic between them, and the drill is in the address
(`/topology?focus=node/R1`): every node on the canvas is a link, open where
something is beneath it and its configuration where nothing is, so cluster to
node to stage to endpoint is a chain of addresses. Every line says what passes
over it — its volume and rate, or `configured · no traffic observed` on a path
configured and never used, drawn dashed — and the inspector lists every link
within the open node with its origin (ADR-0052, amendment 2026-09-25). Open at a
node, the canvas is that node framed with what is beneath it, and nothing
beside it; traffic leaving it runs to one marker labeled *outside*, and the
inspector names that end the same way. The Monitor counts and lists the nodes
the publisher draws as nodes (`IOperatorSurface.NodeScopes`), and its crumb at
the root leads to the tree from its top. What a topology kind, origin or
pattern is called — its word, which styles it, and its name, which a person
reads — is `observe::topology`'s, asked of the runtime through `English`;
the view keeps no word list of its own.

**Every view drills to the problem, and the drill is in the address** (the
owner, 2026-09-26: *it is all about solving the problem*). The Monitor's drill
is `/?scope=<scope>`: it starts at the cluster the publisher publishes at
(`IOperatorSurface.Root`) and never at the root above it, its crumbs, rows,
node rows and *Needs attention* rows are links, each row carries its figures,
and at a scope that has a record of its own — a Location's verdict, or the
leaf at the bottom — that record stands whole above what is beneath it. A
Holding scope's `Why` names its worst leaf as a link to that address, on the
banner and in the Topology's inspector (its open node's *Problem* and each
thing beneath it), so the cause is one click from wherever it is named; the
Configuration tree's *problem* link opens the tree down to the same leaf. A
stage card counts what was taken on its own stage (`IOperatorSurface.Stage`)
and names the Locations configured there (`Locations`), never the cluster's
sum or the leaves beneath it. Each view has one line above its data — the
cluster chooser, what the run was started with, and the source — beneath the
navigation, which carries the logo; the bar that named the view a second time
is gone.

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
this model, and a Playground roll publishes it: the cluster, its nodes, the
stages of the message path each node runs — receive, process, send — one
endpoint per transport beneath a receive or a send stage, and the handoffs
between the nodes, receive to process to send — every pair the roster
configures, and every pair a handoff was delivered over, each link's volume
its hops and its rate their rise per second since the roll's last
publication; the shared store
is drawn when a node ran a test over it (ADR-0052, amendments 2026-09-14,
ruling 3, and 2026-09-19). A snapshot that carries `[run]` says what the run was
started with — tests, cluster, nodes, which are online, the level — in one line
at the top of all three views.
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

It speaks TLS beyond this machine (ADR-0063 clause 1). Plain http is bound on
loopback only — the one exception, which the host says in its log and as an
audit record where it binds it — and any other address in
`[Kestrel.Endpoints]` is `https://`, presenting the PEM certificate and key
`xmip.gui.toml` names as `Certificate` and `PrivateKey`. It asks a caller for
a certificate and checks one presented against `TrustAnchor` (else the
operating system's trust store); a browser may decline and reads the pages,
and the surface hub at `/surface` takes no remote surface over TLS without
one. Plain http beyond loopback, or https with no certificate, is refused
before the host listens (`SurfaceBinding` in `Xmip.Surface.Relay`, over the
one rule in `Xmip.Surface`'s `SurfaceTls`). The TLS is the platform's, through
Kestrel, not `xmip-core-library-tls`.

**Desktop** — `dotnet run --project src/Xmip.Operations -f net11.0-windows10.0.19041.0`.
A native window; needs the maui-windows workload; Windows is the only
platform it carries. Same screen, same operator boundary, so the two cannot
disagree — and the desktop configures: it starts the node its configuration
names, and its Configure page validates and starts through the runtime.

The Configure page edits the one node configuration document
`xmip-core-configure` reads, and holds no model of its own:
`NodeConfiguration` is a view over the document's syntax tree, parsed and
edited by `Xmip.Surface`'s `TomlDocument`, each field one key matched whole,
strings escaped and unescaped by the TOML library. An edit replaces one value
in place; everything else — comments, blank lines, the order of keys and
tables, modules, Subprocesses, Extensions, unknown keys — is written back as
read. A Process it adds writes no `required_modules`, `xmip_subprocesses` or
`extensions`; the document reads them as empty (ADR-0031, amendment
2026-09-24).
It supplies nothing the runtime requires: a Location or Process without
`start`, or a Location without `transport`, is shown as not set and saved
without it. Validate hands the runtime the text the editor holds, saved or
not, through `xmip_validate_v1` (ADR-0027, amendment 2026-09-05), and Save
reports the runtime's verdict on what it wrote; the editor has no verdict of
its own (open problem 25, row b). `src/Xmip.Operations.Test` proves it
against the runtime's own build: a saved document validates, a missing
`start` or `transport` is refused and not defaulted, an escaped string
round-trips, a Process added with only a name and `start` validates, and
comments and layout survive an edit and a save.

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

## What the hosts audit

Both hosts audit through `xmip-core-audit` (ADR-0062), through
`Xmip.Surface`'s `ProgramAudit`, as programs `xmip-gui-web` and
`xmip-operations`, into the directory `[Xmip] AuditDirectory` names in
`xmip.gui.toml`, else the one `XMIP_AUDIT_DIRECTORY` names, else the
operating system's log. Each records its start and stop, every exception
nothing handled, and — through `Xmip.Gui`'s `AuditLoggerProvider`, added to
each host's logging — every error the host logs, with the exception, the
category and the event. A Blazor circuit that dies, which the browser shows
as *An unhandled error has occurred*, is logged by the framework at Error and
so is a record; the web host's `AuditCircuitHandler` records who was
connected — each circuit opened, its connection lost and regained, closed. The
desktop records each validate and start of a node configuration with what
the runtime answered. A web host that cannot start records why, and when the
runtime's library cannot be loaded at all the host writes one entry to the
operating system's log itself, saying so. A Debug build of the web host has
`/debug/unhandled`, which throws, so the path from a failure to its record
can be shown on the real host; a Release build has no such route.
`src/Xmip.Gui.Test`'s `AuditLoggerProviderTest` holds the circuit's case.
