using System.Threading.Tasks;

namespace Coop.Services.Surface
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            SurfaceConfiguration configuration = SurfaceConfiguration.Resolve();
            SurfaceStartup.Announce(configuration);
            SurfaceServer server = SurfaceServer.Create(configuration);
            await server.RunAsync();
            return 0;
        }
    }
}
