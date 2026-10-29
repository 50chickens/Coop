using System.Collections.Generic;
using System.Threading;
using Microsoft.Agents.AI;

namespace Coop.Services.Agent.Tests
{
    public sealed class CoopSkillTests
    {
        [Test]
        public void Test_That_We_Can_Load_The_Skill_Document_Through_The_File_Skills_Source()
        {
            using (AgentFileSkillsSource source = new AgentFileSkillsSource(CoopSkill.Directory))
            {
                AgentSkillsSourceContext context = new AgentSkillsSourceContext(new ChatClientAgent(new StubChatClient(), new ChatClientAgentOptions()), null);

                IList<AgentSkill> skills = source.GetSkillsAsync(context, CancellationToken.None).GetAwaiter().GetResult();
                AgentSkill skill = skills[0];
                string content = skill.GetContentAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

                Assert.That(skills, Has.Count.EqualTo(1));
                Assert.That(skill.Frontmatter.Name, Is.EqualTo("compile-and-run-csharp"));
                Assert.That(skill.Frontmatter.Description, Does.Contain("executing"));
                Assert.That(content, Does.Contain("run_csharp"));
                Assert.That(content, Does.Contain("references/run-csharp-contract.md"));
                Assert.That(skill.GetResourceAsync("references/run-csharp-contract.md", CancellationToken.None).AsTask().GetAwaiter().GetResult(), Is.Not.Null);
            }
        }

        [Test]
        public void Test_That_We_Can_Register_The_Skill_Through_The_Skills_Provider_Builder()
        {
            AgentSkillsProvider provider = new AgentSkillsProviderBuilder().UseCoopSkill().Build();

            Assert.That(provider, Is.Not.Null);
        }
    }
}
