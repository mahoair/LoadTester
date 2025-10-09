namespace LoadTester;

using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using NBomber.CSharp;
using NBomber.Contracts;

// ====== Config ======
internal sealed record LoadConfig(
    string BaseHttp,
    string HubWss,
    string MatchDate,
    int HttpCopies,
    int HttpRampSeconds,
    int HttpHoldSeconds,
    int HubCopies,
    int HubRampSeconds,
    int HubHoldSeconds,
    int HttpTimeoutSeconds,
    bool RunHttp,
    bool RunHub,
    string Profile
)
{
    private static string Env(string k, string d) => Environment.GetEnvironmentVariable(k) ?? d;

    public static LoadConfig FromEnv()
    {
        var profile = Env("LOAD_PROFILE", "baseline").ToLowerInvariant();
        // Profil bazlı defaultlar
        var defaults = profile switch
        {
            "stress" => new { hc = "500", hr = "60", hh = "120", wc = "500", wr = "60", wh = "120", to = "20" },
            "soak"   => new { hc = "200", hr = "60", hh = "900", wc = "200", wr = "60", wh = "900", to = "20" },
            _        => new { hc = "50",  hr = "10", hh = "30",  wc = "50",  wr = "10", wh = "30",  to = "15" }
        };

        var baseHttp = Env("BASE_HTTP", "https://tbfapi.nikayazilim.com/webapi-service/api");
        var hubWss   = Env("HUB_WSS",   "wss://tbfapi.nikayazilim.com/webapi-service/hub");
        var matchDt  = Env("MATCH_DATE", DateTime.UtcNow.Date.ToString("yyyy-MM-dd'T'00:00:00.000'Z'"));

        var httpCopies = int.Parse(Env("HTTP_COPIES", defaults.hc));
        var httpRamp   = int.Parse(Env("HTTP_RAMP_S", defaults.hr));
        var httpHold   = int.Parse(Env("HTTP_HOLD_S", defaults.hh));
        var hubCopies  = int.Parse(Env("HUB_COPIES",  defaults.wc));
        var hubRamp    = int.Parse(Env("HUB_RAMP_S",  defaults.wr));
        var hubHold    = int.Parse(Env("HUB_HOLD_S",  defaults.wh));
        var httpTo     = int.Parse(Env("HTTP_TIMEOUT_S", defaults.to));

        bool runHttp = Env("RUN_HTTP", "1") != "0";
        bool runHub  = Env("RUN_HUB",  "1") != "0";

        return new LoadConfig(
            baseHttp, hubWss, matchDt,
            httpCopies, httpRamp, httpHold,
            hubCopies, hubRamp, hubHold,
            httpTo, runHttp, runHub, profile
        );
    }

    public void Print()
    {
        Console.WriteLine($"PROFILE={Profile}");
        Console.WriteLine($"MATCH_DATE={MatchDate}");
        Console.WriteLine($"RUN_HTTP={RunHttp}, RUN_HUB={RunHub}");
        Console.WriteLine($"HTTP_COPIES={HttpCopies}, HTTP_RAMP_S={HttpRampSeconds}, HTTP_HOLD_S={HttpHoldSeconds}");
        Console.WriteLine($"HUB_COPIES={HubCopies}, HUB_RAMP_S={HubRampSeconds}, HUB_HOLD_S={HubHoldSeconds}");
    }
}

internal static class HttpHelpers
{
    public static async Task<Response<object>> HttpGet(IScenarioContext ctx, HttpClient http, string url)
    {
        try
        {
            using var res = await http.GetAsync(url, ctx.ScenarioCancellationToken);
            return res.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        }
        catch
        {
            return Response.Fail();
        }
    }
}

internal static class HttpSteps
{
    public static Task<Response<object>> Featured9Leagues(IScenarioContext ctx, HttpClient http, string baseHttp)
        => HttpHelpers.HttpGet(ctx, http, $"{baseHttp}/League/get-featured-9-leagues");

    public static Task<Response<object>> DailyMatches(IScenarioContext ctx, HttpClient http, string baseHttp, string matchDate)
        => HttpHelpers.HttpGet(ctx, http, $"{baseHttp}/Match/get-daily-matches?MatchDate={Uri.EscapeDataString(matchDate)}&");

    public static Task<Response<object>> LeagueSponsors(IScenarioContext ctx, HttpClient http, string baseHttp)
        => HttpHelpers.HttpGet(ctx, http, $"{baseHttp}/League/get-league-sponsors?prefix=");

    public static Task<Response<object>> NewsSlider(IScenarioContext ctx, HttpClient http, string baseHttp)
        => HttpHelpers.HttpGet(ctx, http, $"{baseHttp}/News/get-all-news-paginated?page=1&pageSize=5&newsContextId=259&IsSlider=true&");

    public static Task<Response<object>> Stories(IScenarioContext ctx, HttpClient http, string baseHttp)
        => HttpHelpers.HttpGet(ctx, http, $"{baseHttp}/Story/get-stories?");
}

internal static class HubSteps
{
    public static async Task<Response<object>> HubConnectOnce(IScenarioContext ctx, Uri hubUri)
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(hubUri, ctx.ScenarioCancellationToken);

        // SignalR handshake (JSON); sonda 0x1E (\u001e) ayırıcı şart
        var hs = Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e");
        await ws.SendAsync(hs, WebSocketMessageType.Text, true, ctx.ScenarioCancellationToken);

        // Kısa bekleme (mesaj gelirse al)
        var buffer = new byte[4096];
        var receiveTask = ws.ReceiveAsync(buffer, ctx.ScenarioCancellationToken);
        var delayTask   = Task.Delay(2000, ctx.ScenarioCancellationToken);
        await Task.WhenAny(receiveTask, delayTask);

        return Response.Ok();
    }
}

internal static class ScenarioFactory
{
    public static ScenarioProps CreateHttpScenario(LoadConfig cfg, HttpClient http)
    {
        return Scenario.Create("app_boot_http", async context =>
        {
            await Step.Run("featured_9_leagues", context, () => HttpSteps.Featured9Leagues(context, http, cfg.BaseHttp));
            await Step.Run("daily_matches", context, () => HttpSteps.DailyMatches(context, http, cfg.BaseHttp, cfg.MatchDate));
            await Step.Run("league_sponsors", context, () => HttpSteps.LeagueSponsors(context, http, cfg.BaseHttp));
            await Step.Run("news_slider", context, () => HttpSteps.NewsSlider(context, http, cfg.BaseHttp));
            await Step.Run("stories", context, () => HttpSteps.Stories(context, http, cfg.BaseHttp));

            var delay = Random.Shared.Next(100, 500); // 100–500 ms arası
            await Task.Delay(delay, context.ScenarioCancellationToken);
            return Response.Ok();
        })
        .WithLoadSimulations(
            Simulation.RampingConstant(copies: cfg.HttpCopies, during: TimeSpan.FromSeconds(cfg.HttpRampSeconds)),
            Simulation.KeepConstant(copies: cfg.HttpCopies,   during: TimeSpan.FromSeconds(cfg.HttpHoldSeconds))
        );
    }

    public static ScenarioProps CreateHubScenario(LoadConfig cfg)
    {
        return Scenario.Create("live_hub_ws", async context =>
        {
            await Step.Run("hub_connect", context, () => HubSteps.HubConnectOnce(context, new Uri(cfg.HubWss)));

            var delay = Random.Shared.Next(1000, 3000); // 1–3 sn
            await Task.Delay(delay, context.ScenarioCancellationToken);
            return Response.Ok();
        })
        .WithLoadSimulations(
            Simulation.RampingConstant(copies: cfg.HubCopies, during: TimeSpan.FromSeconds(cfg.HubRampSeconds)),
            Simulation.KeepConstant(copies: cfg.HubCopies,   during: TimeSpan.FromSeconds(cfg.HubHoldSeconds))
        );
    }
}

internal static class Program
{
    private static int Main(string[] _)
    {
        var cfg = LoadConfig.FromEnv();
        cfg.Print();

        // Tekil HttpClient (sıkılaştırılmış handler ile)
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = Math.Max(100, cfg.HttpCopies),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        using var http = new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(cfg.HttpTimeoutSeconds);
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "LoadTester/1.0 NBomber");

        var scenarios = new List<ScenarioProps>();
        if (cfg.RunHttp) scenarios.Add(ScenarioFactory.CreateHttpScenario(cfg, http));
        if (cfg.RunHub)  scenarios.Add(ScenarioFactory.CreateHubScenario(cfg));

        if (scenarios.Count == 0)
        {
            Console.WriteLine("No scenarios enabled. Set RUN_HTTP=1 and/or RUN_HUB=1.");
            return 1;
        }

        NBomberRunner
            .RegisterScenarios(scenarios.ToArray())
            .WithReportingInterval(TimeSpan.FromSeconds(5))
            .Run();

        return 0;
    }
}
