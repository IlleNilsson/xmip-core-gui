using System.Collections.Concurrent;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xmip.Gui.Surface;
using Xmip.Gui.Web.Components;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The web host's head is rendered as its routes are, interactive on the
/// server in the one circuit, so the title of the browser's tab is the view
/// the operator moved to (open problem 177). Until 2026-11-25 App.razor gave
/// <see cref="HeadOutlet"/> no render mode where <see cref="Routes"/> was
/// InteractiveServer: the head was rendered once, statically, the host
/// warned on every page that a section's outlet and its content stood in
/// different render modes, and the tab kept the title of the page first asked
/// for.
/// </summary>
public sealed class WebHeadTest : BunitContext
{
    // Where the sections are joined, outlet to content; it warns of a pair
    // standing in two render modes.
    private const string Sections = "Microsoft.AspNetCore.Components.Sections";

    // Made here; the audit beneath it is the host's to make.
    private readonly TestFolder folder = new("gui-web-head");

    private readonly RoleContext role = new(Role.Observer, RoleAssignment.ProcessCaller());

    public WebHeadTest()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.xmip");
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(fixture)));
        Services.AddSingleton(role);
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Unacted.Audit));
        Services.AddScoped<PreferenceCookie>();
        Services.AddScoped<IPreferenceStore>(
            services => services.GetRequiredService<PreferenceCookie>());

        // The head asks the browser for the title the page was served with,
        // and a view's focus is the browser's to move: no browser answers here.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task TheHostRendersTheHeadInteractiveAsItsRoutesAndWarnsOfNoSection()
    {
        Heard heard = new();
        await using WebApplicationFactory<Program> host = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseSetting(ProcessDeclaration.PurposeKey, "test")
                .UseSetting(ProgramAudit.ConfigurationKey, folder.Inside("audit"))
                .ConfigureServices(services => services.AddSingleton<ILoggerProvider>(heard)));
        using HttpClient browser = host.CreateClient();

        string page = await browser
            .GetStringAsync(
                new Uri("monitor", UriKind.Relative),
                Xunit.TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        // The head's content is a component of the circuit, as the routes are:
        // the server's marker stands in the head, before the title it renders.
        using IDocument document = new HtmlParser().ParseDocument(page);
        IElement head = document.Head ?? throw new InvalidOperationException(page);
        INode[] nodes = [.. head.ChildNodes];
        int title = Array.FindIndex(nodes, node => node is IElement { LocalName: "title" });
        int marker = Array.FindIndex(nodes, node => node is IComment comment
            && comment.Data.StartsWith("Blazor:{\"type\":\"server\"", StringComparison.Ordinal));
        Assert.True(marker >= 0 && marker < title, head.InnerHtml);
        Assert.Equal("Xmip — Monitor", document.Title);
        Assert.Empty(heard.Warned(Sections));
    }

    [Fact]
    public void TheTitleFollowsTheViewTheOperatorMovesTo()
    {
        // The head and the routes in one renderer, as the circuit holds them.
        IRenderedComponent<ContainerFragment> app = Render(builder =>
        {
            builder.OpenComponent<HeadOutlet>(0);
            builder.CloseComponent();
            builder.OpenComponent<Routes>(1);
            builder.CloseComponent();
        });
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();

        foreach ((string view, string title) in new[]
        {
            ("monitor", "Xmip — Monitor"),
            ("topology", "Xmip — Communication topology"),
            ("configuration", "Xmip — Configuration"),
        })
        {
            navigation.NavigateTo(view);

            app.WaitForAssertion(() => Assert.Equal(title, app.Find("title").TextContent));
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();
        role.Dispose();
        folder.Dispose();
    }

    // What the host logged at a warning or above, by its category.
    private sealed class Heard : ILoggerProvider
    {
        private readonly ConcurrentQueue<(string Category, string Said)> warned = new();

        public ILogger CreateLogger(string categoryName)
        {
            return new Logger(categoryName, warned);
        }

        public void Dispose()
        {
        }

        // What a category beginning with prefix warned of.
        public string[] Warned(string prefix)
        {
            return
            [
                .. warned
                    .Where(one => one.Category.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(one => $"{one.Category}: {one.Said}"),
            ];
        }

        private sealed class Logger(
            string category, ConcurrentQueue<(string Category, string Said)> warned) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel >= LogLevel.Warning;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                {
                    warned.Enqueue((category, formatter(state, exception)));
                }
            }
        }
    }
}
