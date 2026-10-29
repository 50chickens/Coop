using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Coop.Library.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Coop.Library.Execution
{
    public static class SubmissionCompiler
    {
        public const string AssemblyName = "submission";

        private const string TargetFramework = "net10.0";
        private const string RuntimeFrameworkName = "Microsoft.NETCore.App";
        private const string RuntimeFrameworkVersion = "10.0.0";

        public static CompilationOutcome Compile(string code, string diagnosticsPath, string outputDirectory)
        {
            return Compile(code, diagnosticsPath, outputDirectory, new string[0]);
        }

        public static CompilationOutcome Compile(string code, string diagnosticsPath, string outputDirectory, string[] additionalReferences)
        {
            CompilationOutcome outcome = new CompilationOutcome();
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(code, Encoding.UTF8), new CSharpParseOptions(LanguageVersion.Latest), diagnosticsPath);
            CSharpCompilation compilation = CSharpCompilation.Create(AssemblyName, new SyntaxTree[] { syntaxTree }, References(additionalReferences), CompilationOptions());
            using (MemoryStream assemblyStream = new MemoryStream())
            using (MemoryStream symbolStream = new MemoryStream())
            {
                EmitResult result = compilation.Emit(assemblyStream, symbolStream, options: EmitOptions());
                outcome.Diagnostics = ErrorsOf(result.Diagnostics);
                outcome.Success = result.Success;
                if (!outcome.Success)
                {
                    return outcome;
                }
                outcome.AssemblyPath = WriteArtefact(outputDirectory, assemblyStream.ToArray(), symbolStream.ToArray());
            }
            return outcome;
        }

        private static CSharpCompilationOptions CompilationOptions()
        {
            CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.ConsoleApplication);
            options = options.WithOptimizationLevel(OptimizationLevel.Debug);
            options = options.WithNullableContextOptions(NullableContextOptions.Disable);
            return options;
        }

        private static EmitOptions EmitOptions()
        {
            return new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);
        }

        private static List<MetadataReference> References(string[] additionalReferences)
        {
            List<MetadataReference> references = PlatformReferences();
            if (additionalReferences == null)
            {
                return references;
            }
            additionalReferences
                .Where(path => File.Exists(path))
                .ToList()
                .ForEach(path => references.Add(MetadataReference.CreateFromFile(path)));
            return references;
        }

        private static List<MetadataReference> PlatformReferences()
        {
            return PlatformPaths()
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToList();
        }

        internal static List<string> PlatformPaths()
        {
            string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
            string platformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            return platformAssemblies
                .Split(Path.PathSeparator)
                .Where(path => path.StartsWith(runtimeDirectory, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static List<CompileDiagnostic> ErrorsOf(IEnumerable<Diagnostic> diagnostics)
        {
            return diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => Converted(diagnostic))
                .ToList();
        }

        private static CompileDiagnostic Converted(Diagnostic diagnostic)
        {
            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
            CompileDiagnostic converted = new CompileDiagnostic();
            converted.Id = diagnostic.Id;
            converted.Severity = diagnostic.Severity.ToString();
            converted.Message = diagnostic.GetMessage();
            converted.FilePath = span.Path;
            converted.Line = span.StartLinePosition.Line + 1;
            converted.Column = span.StartLinePosition.Character + 1;
            return converted;
        }

        private static string WriteArtefact(string outputDirectory, byte[] assemblyBytes, byte[] symbolBytes)
        {
            Directory.CreateDirectory(outputDirectory);
            string assemblyPath = Path.Combine(outputDirectory, AssemblyName + ".dll");
            File.WriteAllBytes(assemblyPath, assemblyBytes);
            File.WriteAllBytes(Path.Combine(outputDirectory, AssemblyName + ".pdb"), symbolBytes);
            AtomicFile.Write(Path.Combine(outputDirectory, AssemblyName + ".runtimeconfig.json"), RuntimeConfiguration());
            return assemblyPath;
        }

        private static string RuntimeConfiguration()
        {
            return "{\"runtimeOptions\":{\"tfm\":\"" + TargetFramework + "\",\"framework\":{\"name\":\"" + RuntimeFrameworkName + "\",\"version\":\"" + RuntimeFrameworkVersion + "\"},\"rollForward\":\"LatestMinor\"}}";
        }
    }
}
