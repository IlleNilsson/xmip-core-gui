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

Seven views, and seven points to drill from, in this order on the bar:
**Configuration**, the classic tree, holding still; **Monitor**, the board
that follows Receive → Process → Send; **Topology** (ADR-0052, amendments
2026-09-14 and 2026-09-18); **Subscriptions**, what picks a published Message
up and opens a Journey (ADR-0013, amendment 2026-09-30); **Dead Message
Queue**, the accepted Messages no Subscription matched (ADR-0052, amendment
2026-10-01); **Event subscriptions**, who hears what Xmip did (ADR-0065,
amendment 2026-09-29); and **Audit**, what every Xmip program recorded
(ADR-0062, amendment 2026-09-29) — the last four described below, the
Subscriptions placed before the Event subscriptions by ADR-0052, amendment
2026-09-30, and the Dead Message Queue after the Subscriptions, whose
declines it shows. A
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
within the open node with its origin (ADR-0052, amendment 2026-09-25). A line
is selected by a click or from the keyboard: each is a button in the tab
order, named for its pattern and its ends and taken by Enter or Space, and
each link's ends in the inspector's Traffic list is a button that selects the
same line as text. Open at a
node, the canvas is that node framed with what is beneath it, and nothing
beside it; traffic leaving it runs to one marker labeled *outside*, and the
inspector names that end the same way. Where the publisher draws Parties
(ADR-0052, amendment 2026-09-29) the cluster reads left to right as the
streams run: a Party that sends stands left of the nodes it sends into, one
delivered to right of the nodes that deliver to it, and the nodes between in
columns by their links; opened, a Party is drawn where it stands, selected,
and the inspector lists every transport it uses with its state. The Monitor counts and lists the nodes
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
sum or the leaves beneath it. What a card says a stage is moving is the rate
per second the one `FigureWatch` in `Xmip.Surface` gives between two
publications, the prompt's own, and written by `English.Flow`; until
2026-09-27 the board kept its own last value and delta. Each view has one line
above its data — the
cluster chooser, what the run was started with, and the source — beneath the
navigation, which carries the logo; the bar that named the view a second time
is gone. The navigation is one component for both hosts, `TopNav`: the seven
views, what the host adds after them — the desktop's Configure, for a role
that may configure — and the role it runs as, described once
(`Roles.Describe`). The filter box and what it narrows are the views'
base, `ClusterView`, once.

The run line carries one checkbox on every view, *show test clusters*, beside
the cluster chooser (the owner, 2026-09-29; ADR-0052, amendment 2026-09-30).
Off — the default — a cluster whose run declared itself hidden
(`Start-XmipTest -Hidden`) is not in the chooser, not reached by an address
that names it, and its audit records are neither counted nor grouped; on, it
is, and its pill, its audit group and its records say `· test`, outlined
dashed. Hidden is what the run declared, never its name (`ClusterSurfaces`).
The box is in the address, `hidden=include`, and every link a view writes
carries it beside the cluster (`Carry`, `ScopeLink`), so a link reproduces the
view.

**The Audit view** (`/audit`; the owner, 2026-09-29: *audited entries in the
clusters. Drill-down, sorting and filtering*) reads the records every Xmip
program audited, through `ProgramAudit.Read` in `Xmip.Surface` and the audit
capability's one reader and query behind `xmip_audit_read_v1` — the page
filters, sorts and pages nothing itself. Who a record is, is the location its
process declared (ADR-0053 clause 3): the drill runs cluster → node → program
→ one record, and a program that declared none — a cmdlet, this host — stands
under its host; nothing is read out of a program's name. Each level lists the
groups one step down with their counts, errors and warnings, then the records:
time (UTC), node, program, action, phase, severity and summary, newest first,
a column's head sorting by it and again the other way. The filters are the
scope pattern box every view has — the one wildcard, `observe::wildcard`,
over each record's location — and severity, action and a time range beside
it. Everything is in the address (`/audit?location=xmip:///C1/node/R1&
severity=error&sort=action&order=ascending`), so a link reproduces the view;
a page draws 200 rows and links the next and the previous (no `Virtualize`,
which prerendered a blank spacer). A record opens whole, every property with
it. The view reads the audit of the machine its host runs on — the directory
`Xmip:AuditDirectory` names, else `XMIP_AUDIT_DIRECTORY` — and says so in
words where there is none, where the runtime's library cannot be loaded, and
over a remote surface, whose cluster's audit stays on its own machine. The
Monitor's drill links each scope to its audit beside its configuration.

**The Subscriptions view** (`/subscriptions`; the owner, 2026-09-30: *a
Subscription view, for all subscriptions per cluster, with pause and resume,
but not remove. That is handled with the TOML configuration files*) lists
every Subscription the cluster's nodes route by, through
`IOperatorSurface.Subscriptions`: its configured name, the cluster and the
node, its filter — what it subscribes to — and where it leads, the Xmip
Process or Send Port it opens a Journey into; then its state, active or
paused, what it picked up, what it holds and since. A paused one is the Paused
mood, painted as the Monitor paints it and said in words. It drills cluster →
node → one Subscription by name, a column's head orders by it and again the
other way, and the pattern box narrows over each one's node and name — all
`SubscriptionQuery`'s, all in the address (`/subscriptions?location=
xmip:///C1/node/R1&name=edi`). The one opened shows its configuration exactly
as the TOML says it, the file it comes from, the Xmip Application that draws
it, and who paused it while it is paused. An Operator is offered Pause while
it picks up — what it matches is held, not picked up, and nothing is lost —
and Resume while it holds — what it held is picked up, oldest first — on every
row and on the one opened; there is no Remove anywhere, and the view says so
in words (`SubscriptionOperation.Configured`): a Subscription is added and
removed in the TOML configuration of the Xmip Application that draws it. Each
act goes through `IOperatorSurface.Act` — applied in the node's process, or
over a snapshot left where its publication says its publisher takes orders —
and is recorded in the host's audit as `subscription.<act>`. An Observer is
shown the list and no act (ADR-0013, amendment 2026-09-30).

**The Dead Message Queue view** (`/dead-messages`; ADR-0052, amendment
2026-10-01) lists what each node's Dead Message Queue keeps, through
`IOperatorSurface.DeadMessages`: every accepted Message that no Subscription
matched, kept in the Ledger with its receive context — when it was received,
its identifier, the cluster and the node whose queue keeps it, the Receive
Location it arrived at and how many Subscriptions declined it, the oldest
first; a node publishes its oldest hundred. It is not a dead letter queue: a
failed Journey never goes there, so an entry is no mood and its row is not
painted. It drills cluster → node → one Message, a column's head orders by
it and again the other way, and the pattern box narrows over each one's node
and Message — all `DeadMessageQuery`'s, all in the address
(`/dead-messages?location=xmip:///C1/node/R1&message=<id>`). The one opened
shows its gate verdicts in the order they ran, its promoted properties and
every Subscription asked and why it declined, in the order asked. An Operator
is offered Replay on every row and on the one opened — once a Subscription is
added or fixed, the Message is routed again against the node's Subscriptions
of now, a Journey opened for each match and the entry taken out; one that
still matches nothing stays. The Replay goes through `IOperatorSurface.Act`
— applied in the node's process, or over a snapshot left where its
publication says its publisher takes orders — and is recorded in the host's
audit as `dead-message.replay`. An Observer is shown the list and no act.

**The Journeys that failed** (runtime-model.md section 13; ADR-0013) are
listed and acted on where the Monitor shows them: a Send Port's own record,
at `<node>/send/<Port>`, says in its evidence what the Port sent, what
failed, how many failed wait in its queue and the last that failed and why.
Drilled to that scope, every Journey that failed there is listed with its
identifier and why — read through `IOperatorSurface.FailedJourneys`, a page
at a time from Xmip Storage in the node's process with More for the next,
or the oldest hundred a snapshot's publication carries — how many wait, and
for each an Operator is offered Retry — sent again, its tries begun anew —
and Dismiss — given up, written Dismissed and taken out of the Port's queue
(`JourneyActs.razor`). A surface that lists none offers the one Journey the
evidence names (`JourneyOperation.FailedIn`, the one reader of that clause).
The act goes through `IOperatorSurface.Act` — applied in the node's process,
or over a snapshot left where its publication says its publisher takes
orders — and is recorded in the host's audit as `journey.retry` or
`journey.dismiss`. An Observer is shown the Journeys and no act.

**The Event subscriptions view** (`/event-subscriptions`; the owner,
2026-09-29: *a view of event subscriptions. Subscriber, Cluster, Node, Action.
One should be able to pause, resume and remove event subscriptions*) lists
every Event subscription the cluster's nodes hold, through
`IOperatorSurface.EventSubscriptions`: the subscriber, a Party; the cluster and the
node whose hub holds it — the one its subscriber connected to, not the one it
hears, for it hears the matching Events of every node of the cluster (ADR-0065,
amendment 2026-10-02), and the links that carry them between nodes are the
cluster's and never listed; the action it subscribes to; then its state, queued
against capacity, delivered, missed and since. Above them, read-only, one line
for each member a node there does not hear — *R1: not hearing `<node>`
since `<time>`: `<why>`*, `EventSubscriptionQuery.Line` — with no act, so no
Event is missing silently. A paused one is the Paused
mood, painted as the Monitor paints it and said in words. It drills cluster →
node → one Event subscription, a column's head orders by it and again the
other way, and the pattern box narrows over each one's node and reach — all
`EventSubscriptionQuery`'s, all in the address (`/event-subscriptions?location=
xmip:///C1/node/R1&id=2`). An Operator is offered Pause or Resume, and Remove,
on every row and on the one opened; each act goes through `IOperatorSurface.Act`
— applied in the node's process, or over a snapshot left where its publication
says its publisher takes orders — and is recorded in the host's audit as
`event.<act>`. An Observer is shown the list and no act (ADR-0065,
amendment 2026-09-29). The three views share their pieces — the drill's crumbs
(`DrillTrail`), the nodes one step down (`NodeGroups`), the sortable head
(`SortHead`), what an act came to (`ActNotes`, `ActSaid`) — so they cannot
drift apart.

`src/Xmip.Gui.Test` holds the pages' tests (ADR-0052 clause 6): bUnit renders
the views over the surface library's own published fixture, the Audit view
over an audit file in the capability's shape, and the Subscriptions, the
Dead Message Queue and the Event subscriptions views over a publication
whose publisher takes orders, so what is asserted is what an operator sees
and no surface is faked.

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
at the top of every view.
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
The web GUI offers what the desktop offers, by role (ADR-0014 and ADR-0052,
amendments 2026-09-14): an Observer watches, an Operator also pauses, resumes
and removes. It starts no node. Both hosts take the role by one rule,
`RoleContext.Assigned` (in `Xmip.Surface`): `Role` in `xmip.gui.toml`, else
`XMIP_ROLE`, and a word that is no role is Observer; with none stated and no
directory configured the tester holds every role (ADR-0009, amendment
2026-09-14). The same role is enforced on what other machines ask: the surface
hub takes an act — pause, resume, remove — only where the host's role may act,
and as the subject of the client certificate that reached it, never a name the
caller gives — on loopback without a certificate, as the operating system
user the host runs as; it refuses in words an act its role may not take or
one from elsewhere without a certificate, and audits every one (ADR-0009, amendment 2026-10-03).

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
disagree — and the desktop configures: it edits the cluster's `xmip.toml`,
slices it for each node on save, and plans the node of it its configuration
names — validated and its plan published; nothing runs it here.

**The Configure page edits the cluster's `xmip.toml` and nothing else**
(ADR-0031, amendment 2026-10-05: *node TOML files shall not be edited, only
cluster TOML files, the files are sliced / node and distributed*). It opens
on an overview of the cluster — its shared sections (`[service]`,
`[tuning]`, `[storage]`, `[store]`) and its nodes — and drills down to a
node, with its own values and its artifacts, and to the artifacts by kind:
Receive Port, Receive Location, Send Port, Send Location, Send Port Group,
Prepare, Promote, Demote, Route (an Xmip Application, its routes listed as
entries; the route canvas is the VS Code designer's), Transformation and
Process — whatever the runtime's views answer. A kind the configuration
does not define yet shows the runtime's note and nothing to edit. An entry
opens with its values edited in place as TOML (`"text"`, `4`, `true`),
values added and removed, entries added — with the values they must hold,
which the runtime names where one is missing — and removed, nodes added
and removed. Every value, key and entry control carries an accessible name
for what it edits (`name value`, `Remove start`, `Open drop, cluster`), and a
removal, like every edit, is undone until saved by **Discard changes**.

**Edits not saved are never lost by leaving, and never overwrite another
editor's.** While the text holds edits, *changed, not saved* shows beside
**Discard changes**, which reads the file again as it is on disk; leaving the
page is held, and the page asks: **Save and leave**, **Discard and leave** or
**Stay** (Blazor's `NavigationLock`; a browser's own close asks too). The
page keeps the SHA-256 of the file as it read or last wrote it, and a save
first compares the disk against it: a file another editor saved meanwhile is
not written over — the save is refused in words, audited, and offers
**Reload from disk** — the same ground the VS Code designer refuses an edit
on, by the document's version. A save writes a temporary file of its own
beside the target (`xmip.toml.<guid>.writing`), flushes it to the device and
renames it over the file, so a reader never sees half of one and two saves
never share a temporary file.

The page holds no configuration rule. Every view, edit and slice is the
runtime's, through `xmip_operate.h` section 10, bound once in `Xmip.Abi`'s
`RuntimeDesign` (`RuntimeRules.Design`) and read in `Xmip.Surface` as
`ConfigurationViews` (`ArtifactView`, `ArtifactEntry`, `ArtifactField`,
`ArtifactPlace`), `ClusterEdit` and `NodeSlice` — the views and edits the VS
Code designer's language server asks for (`vscode/README.md`). An edit is
made by `xmip_cluster_edit_v1`, comments and layout kept, and one that would
leave a node unable to read its slice is refused in the runtime's words and
audited. A node's own document is never offered for editing: the views say
which document a text is, and the slicing's refusal is shown in its place.
Validate hands the runtime the text the editor holds, saved or not, through
`xmip_validate_v1`, which reads a cluster's file node by node (ADR-0027,
amendment 2026-09-05).

**Save slices and ships** (the owner, 2026-10-05: *When editing is done, the
cluster TOML file is sliced into node TOML files and shipped to each node on
save*). Save writes the cluster's file whole, has the runtime judge what is
on disk, then slices it for each node it declares through the one slicing,
`xmip_cluster_slices_v1` (`configure::slice`), and writes each slice as
`<SliceDirectory>/<node>/xmip-node.toml`, the name desired state writes it
by. The page shows each node's outcome: *shipped* for the node this desktop
starts, whose slice is written where it starts from and takes effect when it
starts again; *sliced, not shipped* for every other node, since Xmip has no
path yet that puts a file on another node — desired state slices the
cluster's file on the node as it deploys it (the Ansible role `xmip_node`,
`deploy/dsc/xmip-node.dsc.yaml`), and a node's operate listener (ADR-0067)
takes no configuration; or *refused*, with the runtime's sentence, for a
node that does not slice, which stops no other node. **Plan** slices the saved
file for the node configured here and hands its slice to `xmip_start_v1`,
only when an operator presses it, never at launch: the runtime reads,
validates and publishes the node's plan, which the board then shows, and
runs nothing — a node runs in the program that links its technologies
(ADR-0018, amendment 2026-09-26). The page says *planned … not running*, the
node's badge says *configured here*, and nothing on it says started until
there is lifecycle control that starts one.

`src/Xmip.Operations.Test` proves it against the runtime's own build: the
sample cluster, `samples/xmip.toml`, opens and validates; a value edited in
place keeps every comment; an entry missing what it must hold is refused
naming it, and added with it; a node is added; a refused edit leaves the text
as it was; a node's own document is not editable; a cluster begun in an
empty file becomes one when it declares a node; each node is sliced, the
desktop's shipped and another not, and a node that does not slice is refused
without stopping the others; a save over a file another editor changed, or
one that appeared since an empty open, is refused and writes nothing, and a
save leaves no temporary file; and every save, slice, ship, refused edit and
plan lands in the audit. `ConfigurePageTest` renders the page with bUnit,
compiling its Razor components where they live as it compiles the
Configuration folder: leaving with edits is held and Stay keeps them, Discard
and leave drops them, Save and leave writes them, leaving with nothing
changed is not held, Discard changes is offered only while there are edits,
a stale save offers a reload, the node is planned and never said to start,
and every control has its accessible name.

Configuration is each host's `xmip.gui.toml`, with the same keys under
`[Xmip]`: `Surface = "native" | "snapshot" | "remote"`, `RuntimeLibrary`
(else `XMIP_RUNTIME_LIBRARY`, else beside the executable), `Snapshot = <path>`
when the surface is a snapshot — a path with no file behind it is said so on
the page — `Url = <web host>` when the surface is remote, `Role`, and, on the
desktop only, `ClusterConfiguration` (the cluster's `xmip.toml`, else
`xmip/xmip.toml` under the app's data), `Node` (the node of it configured
here, planned only by Plan; none when unset) and `SliceDirectory` (where each node's slice is
written, else `xmip/slices` under the app's data). The web host serves its own surface at
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
desktop records each save of the cluster's `xmip.toml`, each node's slice and
its ship, every edit the runtime refused, and each validate and plan of a
node, with what the runtime answered. A web host that cannot start records why, and when the
runtime's library cannot be loaded at all the host writes one entry to the
operating system's log itself, saying so. A Debug build of the web host has
`/debug/unhandled`, which throws, so the path from a failure to its record
can be shown on the real host; a Release build has no such route.
`src/Xmip.Gui.Test`'s `AuditLoggerProviderTest` holds the circuit's case.
