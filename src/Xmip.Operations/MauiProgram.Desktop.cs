using System.Reflection;
using Xmip.Surface;

namespace Xmip.Operations;

/// <summary>
/// The desktop's part of the start: it monitors and configures (ADR-0014).
/// Compiled for Windows only; the phone observes and nothing else.
/// </summary>
public static partial class MauiProgram
{
    private static partial ProgramAudit Platform(MauiAppBuilder builder)
    {
        // TOML beside the executable, like the web host — Xmip configures
        // nothing in JSON — through the one reader every surface uses. The
        // file is deployed as content next to the app.
        string here = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;
        TomlDocument.Add(
            builder.Configuration, Path.Combine(here, "xmip.gui.toml"), optional: true);

        // Relative paths in the config resolve by the one rule the web host
        // shares: the estate root in a development build, the app's own
        // directory once packaged.
        string basePath = TomlDocument.BasePath(here);
        string library = RuntimeLibrary.Find(builder.Configuration, basePath);

        // What this process says of itself while it runs (ADR-0053), through
        // the runtime library this desktop was told to load. The desktop
        // configures and monitors a real node: runtime.
        ProcessDeclaration.Declare(Name, ScopeTree.Root, ProcessDeclaration.Runtime, library);

        // Into the directory xmip.gui.toml names, else where the capability
        // decides (ADR-0062).
        ProgramAudit audit = new(Name, ProgramAudit.Stated(builder.Configuration, basePath));

        // The role is assigned, never chosen in the UI (ADR-0009): a person
        // cannot promote themselves. The caller is the operating system user
        // running the desktop, granted a role by the one rule both hosts and
        // the hub take it by (ADR-0009, amendment 2026-10-06).
        builder.Services.AddSingleton(
            RoleAssignment.From(builder.Configuration).For(GatedOperator.HostUser));

        // The cluster's xmip.toml the Configure page edits — the one file
        // anyone edits (ADR-0031, amendment 2026-10-05) — where each node's
        // slice is written on save, and the node of it configured here:
        // all three as xmip.gui.toml declares them, never read from a name.
        SliceDelivery delivery = SliceDelivery.From(
            builder.Configuration,
            basePath,
            Path.Combine(FileSystem.AppDataDirectory, "xmip"));

        // The surfaces every screen reads, chosen in xmip.gui.toml and never
        // guessed (ADR-0052 clause 3), by the web host's one rule: one
        // snapshot per cluster where several are named, so the desktop moves
        // between clusters as the web does (the owner, 2026-10-02: "make it so
        // that the desktop version opens multiple clusters too"). Nothing is
        // planned at launch: nothing starts itself, and a node is planned only
        // when an operator presses Plan on the Configure page.
        builder.Services.AddSingleton(_ => SurfaceChoice.OpenAll(builder.Configuration, basePath));

        // Configure's commands answer one surface: the first cluster's
        // (SurfaceChoice.OpenFirst's rule), the one a node is planned on.
        builder.Services.AddSingleton(services =>
            services.GetRequiredService<ClusterSurfaces>().First);

        // Validate, Save and Plan on the Configure page have no node behind
        // them: the runtime found by the one rule is loaded for them alone
        // (ADR-0014, amendment 2026-10-13).
        builder.Services.AddSingleton(new RuntimeCommands(library, audit, delivery));

        return audit;
    }
}
