using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using storemkt.library;

public class Program
{
    public static void Main(string[] args)
    {
        SampleClass sampleClass = new SampleClass();
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
}
