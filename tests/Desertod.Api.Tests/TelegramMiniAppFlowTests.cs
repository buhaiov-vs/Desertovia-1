using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class TelegramMiniAppFlowTests(DesertodApiFactory factory)
{
    private string Data(string id,string token)
    {
        var user=JsonSerializer.Serialize(new { id=long.Parse(id),username="isolated_mini" });
        var fields=new SortedDictionary<string,string>(StringComparer.Ordinal) { ["auth_date"]=factory.Clock.GetUtcNow().ToUnixTimeSeconds().ToString(),["user"]=user };
        var key=HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"),Encoding.UTF8.GetBytes(token));
        var hash=Convert.ToHexStringLower(HMACSHA256.HashData(key,Encoding.UTF8.GetBytes(string.Join('\n',fields.Select(f=>$"{f.Key}={f.Value}")))));
        return string.Join('&',fields.Select(f=>$"{f.Key}={WebUtility.UrlEncode(f.Value)}"))+"&hash="+hash;
    }
    [Fact]
    public async Task Mini_app_and_widget_use_the_same_account()
    {
        var id=Random.Shared.NextInt64(10000,long.MaxValue).ToString();
        using var client=factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect=false });
        var login=await client.PostAsJsonAsync("/auth/telegram/miniapp",new {initData=Data(id,DesertodApiFactory.TelegramBotToken)});
        Assert.Equal(HttpStatusCode.NoContent,login.StatusCode);
        var mini=await client.GetFromJsonAsync<JsonElement>("/api/me");
        var fields=TelegramLogin.Signed(DesertodApiFactory.TelegramBotToken,TelegramLogin.Fields(id,factory.Clock.GetUtcNow(),username:"isolated_mini"));
        var query=string.Join('&',fields.Select(f=>$"{f.Key}={WebUtility.UrlEncode(f.Value)}"));
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/auth/telegram/callback?"+query)).StatusCode);
        var widget=await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal(mini.GetProperty("id").GetString(),widget.GetProperty("id").GetString());
    }
    [Fact]
    public async Task Mounted_path_keeps_api_and_cookie_together()
    {
        await using var mounted=factory.WithWebHostBuilder(builder => builder.UseSetting("Hosting:PathBase","/dezertovia"));
        using var client=mounted.CreateClient();
        var r=await client.PostAsJsonAsync("/dezertovia/auth/telegram/miniapp",new {initData=Data("1002003",DesertodApiFactory.TelegramBotToken)});
        Assert.Equal(HttpStatusCode.NoContent,r.StatusCode);
        Assert.Contains(r.Headers.GetValues("Set-Cookie"),value=>value.Contains("path=/dezertovia"));
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/dezertovia/api/me")).StatusCode);
    }
    [Fact]
    public async Task Forged_mini_app_does_not_open_a_session()
    {
        using var client=factory.CreateClient();
        var r=await client.PostAsJsonAsync("/auth/telegram/miniapp",new {initData=Data("1001","wrong:token")});
        Assert.Equal(HttpStatusCode.Unauthorized,r.StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/me")).StatusCode);
    }
}
