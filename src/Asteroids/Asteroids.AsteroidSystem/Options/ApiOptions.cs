namespace Asteroids.AsteroidSystem.Options;

public class ApiOptions
{
    public string RaftStorageUrl { get; set; } = "http://storage-api:8080/gateway";
    public string AkkaRoles { get; set; } = "System";
    public string AkkaSeeds { get; set; } = "akka.tcp://MyAsteroidSystem@lighthouse:4053";
    public string AkkaHostname { get; set; } = "localhost";
}

public static class ApiOptionsExtensions
{
    public static WebApplicationBuilder AddApiOptions(this WebApplicationBuilder builder)
    {
        var apiOptions = new ApiOptions();
        builder.Configuration.Bind(nameof(ApiOptions), apiOptions);
        builder.Services.AddSingleton(apiOptions);
        return builder;
    }
}
