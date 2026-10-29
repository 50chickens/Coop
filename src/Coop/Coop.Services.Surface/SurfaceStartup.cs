using System;
using System.Collections.Generic;

namespace Coop.Services.Surface
{
    public static class SurfaceStartup
    {
        public static void Announce(SurfaceConfiguration configuration)
        {
            List<string> names = ToolCatalog.Names();
            Console.Error.WriteLine(SurfaceServer.ServerName + " " + configuration.Version);
            Console.Error.WriteLine("data root: " + configuration.DataRoot);
            Console.Error.WriteLine("record store root: " + configuration.RecordStoreRoot);
            Console.Error.WriteLine("tools (" + names.Count + "): " + string.Join(", ", names));
            Console.Error.WriteLine("ready");
            Console.Error.Flush();
        }
    }
}
