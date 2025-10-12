using Microsoft.Extensions.Configuration;
using System.IO;
using System.Windows;

namespace ReplaysApp
{
    public partial class App : Application
    {
        public static IConfiguration Configuration { get; private set; }

        public App()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddUserSecrets<App>();
            Configuration = builder.Build();
        }
    }
}