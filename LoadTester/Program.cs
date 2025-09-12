using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using NBomber.CSharp;
using NBomber.Contracts;

// ====== Config (env ile override edilebilir) ======
static string Env(string k, string d) => Environment.GetEnvironmentVariable(k) ?? d;

var BASE_HTTP = Env("BASE_HTTP", "https://tbfapi.nikayazilim.com/webapi-service/api");
var HUB_WSS   = Env("HUB_WSS",   "wss://tbfapi.nikayazilim.com/webapi-service/hub");

// Günlük maç tarihi (UTC 00:00)
var MATCH_DATE = Env("MATCH_DATE", DateTime.UtcNow.Date.ToString("yyyy-MM-dd'T'00:00:00.000'Z'"));
Console.WriteLine(MATCH_DATE);

// HTTP yükü
var HTTP_COPIES  = int.Parse(Env("HTTP_COPIES", "50"));
Console.WriteLine(HTTP_COPIES);
var HTTP_RAMP_S  = int.Parse(Env("HTTP_RAMP_S", "30"));
Console.WriteLine(HTTP_RAMP_S);
var HTTP_HOLD_S  = int.Parse(Env("HTTP_HOLD_S", "120"));
Console.WriteLine(HTTP_HOLD_S);

// HUB yükü
var HUB_COPIES   = int.Parse(Env("HUB_COPIES", "100"));
Console.WriteLine(HUB_COPIES);
var HUB_RAMP_S   = int.Parse(Env("HUB_RAMP_S", "60"));
Console.WriteLine(HUB_RAMP_S);
var HUB_HOLD_S   = int.Parse(Env("HUB_HOLD_S", "180"));
Console.WriteLine(HUB_HOLD_S);

// Tek HttpClient (önerilir)
var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

// ---- yardımcılar (v6: Response.Ok()/Fail() parametresiz kullan) ----
static async Task<Response<object>> HttpGet(IScenarioContext ctx, HttpClient http, string url)
{
    using var res = await http.GetAsync(url, ctx.ScenarioCancellationToken);
    return res.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
}

static async Task<Response<object>> HubConnectOnce(IScenarioContext ctx, Uri hubUri)
{
    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(hubUri, ctx.ScenarioCancellationToken);

    // SignalR handshake (JSON); sonda 0x1E (\u001e) ayırıcı şart
    var hs = Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e");
    await ws.SendAsync(hs, WebSocketMessageType.Text, true, ctx.ScenarioCancellationToken);

    // (opsiyonel) burada Join/Subscribe invocation çerçevesi gönderebilirsin:
    // var join = "{\"type\":1,\"target\":\"JoinMatch\",\"arguments\":[\"match-123\"],\"invocationId\":\"1\"}\u001e";
    // await ws.SendAsync(Encoding.UTF8.GetBytes(join), WebSocketMessageType.Text, true, ctx.ScenarioCancellationToken);

    // kısa bekleme (mesaj gelirse al)
    var buffer = new byte[4096];
    var receiveTask = ws.ReceiveAsync(buffer, ctx.ScenarioCancellationToken);
    var delayTask   = Task.Delay(2000, ctx.ScenarioCancellationToken);
    await Task.WhenAny(receiveTask, delayTask);

    return Response.Ok();
}

// ---- SCENARIO 1: App açılışı HTTP istekleri ----
var scenarioHttp = Scenario.Create("app_boot_http", async context =>
{
    await Step.Run("featured_9_leagues", context, () =>
        HttpGet(context, http, $"{BASE_HTTP}/League/get-featured-9-leagues"));

    await Step.Run("daily_matches", context, () =>
        HttpGet(context, http,
            $"{BASE_HTTP}/Match/get-daily-matches?MatchDate={Uri.EscapeDataString(MATCH_DATE)}&"));

    await Step.Run("league_sponsors", context, () =>
        HttpGet(context, http, $"{BASE_HTTP}/League/get-league-sponsors?prefix="));

    await Step.Run("news_slider", context, () =>
        HttpGet(context, http,
            $"{BASE_HTTP}/News/get-all-news-paginated?page=1&pageSize=5&newsContextId=259&IsSlider=true&"));

    await Step.Run("stories", context, () =>
        HttpGet(context, http, $"{BASE_HTTP}/Story/get-stories?"));
    
    var delay = Random.Shared.Next(100, 500); // 100–500 ms arası
    await Task.Delay(delay, context.ScenarioCancellationToken);

    return Response.Ok();
})
.WithLoadSimulations(
    Simulation.RampingConstant(copies: HTTP_COPIES, during: TimeSpan.FromSeconds(HTTP_RAMP_S)),
    Simulation.KeepConstant(copies: HTTP_COPIES,   during: TimeSpan.FromSeconds(HTTP_HOLD_S))
);

// ---- SCENARIO 2: Hub’a WS bağlan + handshake ----
var scenarioHub = Scenario.Create("live_hub_ws", async context =>
{
    await Step.Run("hub_connect", context, () => HubConnectOnce(context, new Uri(HUB_WSS)));
    
    var delay = Random.Shared.Next(1000, 3000); // 1–3 sn
    await Task.Delay(delay, context.ScenarioCancellationToken);
    return Response.Ok();
})
.WithLoadSimulations(
    Simulation.RampingConstant(copies: HUB_COPIES, during: TimeSpan.FromSeconds(HUB_RAMP_S)),
    Simulation.KeepConstant(copies: HUB_COPIES,   during: TimeSpan.FromSeconds(HUB_HOLD_S))
);

// ---- Koşu ----
NBomberRunner
    .RegisterScenarios(scenarioHttp, scenarioHub)
    .WithReportingInterval(TimeSpan.FromSeconds(5))   // v6: min 5 sn
    .Run();
