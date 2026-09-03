# xmip-core-gui

The graphical operator surface: a Blazor component library over the Xmip ABI,
with two hosts — .NET 11 MAUI on the desktop and .NET 11 Blazor on the web.

One component library, two hosts, zero drift: both render the same components
calling the same ABI, per ADR-0014. What the desktop shows and what the web
shows cannot disagree, because there is only one implementation to disagree
with.

## State

Declared, empty, and honestly so: `maturity = "planned"` in
`architecture.toml`. The market survey calls this gap wider than any open
runtime feature — every competitor leads with a visual designer — so when
work starts here, it starts with that weight on it.
