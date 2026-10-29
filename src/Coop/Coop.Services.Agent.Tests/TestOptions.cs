using System.IO;

namespace Coop.Services.Agent.Tests
{
    public static class TestOptions
    {
        public static CoopExecutionOptions Create()
        {
            CoopExecutionOptions options = new CoopExecutionOptions();
            options.WorkingRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "runs");
            options.TimeoutSeconds = 30;
            return options;
        }
    }
}
