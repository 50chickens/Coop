using System.IO;

namespace Coop.Library.Execution
{
    public static class SourceResolver
    {
        public const string DefaultDiagnosticsPath = "submission.cs";

        public static SourceResolution Resolve(Submission submission)
        {
            SourceResolution resolution = new SourceResolution();
            bool hasSourceText = submission.SourceText != null;
            bool hasSourcePath = submission.SourcePath != null;
            if (!hasSourceText && !hasSourcePath)
            {
                resolution.RefusalName = SubmissionRefusalNames.NoSourceSupplied;
                resolution.RefusalMessage = "the submission supplies neither sourceText nor sourcePath";
                return resolution;
            }
            if (hasSourceText)
            {
                resolution.Code = submission.SourceText;
                resolution.CodeField = "sourceText";
                resolution.DiagnosticsPath = hasSourcePath ? submission.SourcePath : DefaultDiagnosticsPath;
                return resolution;
            }
            resolution.Code = File.ReadAllText(submission.SourcePath);
            resolution.CodeField = "sourcePath";
            resolution.DiagnosticsPath = submission.SourcePath;
            return resolution;
        }
    }
}
