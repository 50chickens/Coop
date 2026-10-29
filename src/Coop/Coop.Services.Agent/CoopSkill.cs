using System;
using System.IO;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace Coop.Services.Agent
{
    public static class CoopSkill
    {
        public const string Name = "compile-and-run-csharp";

        public static string Directory
        {
            get
            {
                return Path.Combine(AppContext.BaseDirectory, "Skills", Name);
            }
        }

        public static AgentSkillsProviderBuilder UseCoopSkill(this AgentSkillsProviderBuilder builder)
        {
            return builder.UseSource(loggerFactory => new AgentFileSkillsSource(Directory, null, null, loggerFactory));
        }
    }
}

