using NBomber.CSharp;
using System.Net.Http;

var scenario = Scenario.Create("api_test", async context =>
    {
        // Step: API çağrısı
        var step = await Step.Run("get_post", context, async () =>
        {
            using var client = new HttpClient();
            var res = await client.GetAsync("https://apigw.trendyol.com/discovery-web-accountgw-service/api/locations/cities?culture=tr-TR&storefrontId=1&channelId=1", context.ScenarioCancellationToken);

            if (res.IsSuccessStatusCode)
                return Response.Ok(statusCode: ((int)res.StatusCode).ToString());

            return Response.Fail(statusCode: ((int)res.StatusCode).ToString());
        });

        return Response.Ok();
    })
    .WithLoadSimulations(
        // 10 saniyede 10 eşzamanlı kullanıcıya rampa
        Simulation.RampingConstant(copies: 10, during: TimeSpan.FromSeconds(10))
    );

NBomberRunner
    .RegisterScenarios(scenario)
    .WithReportingInterval(TimeSpan.FromSeconds(5)) // v6’da min 5 sn
    .Run();