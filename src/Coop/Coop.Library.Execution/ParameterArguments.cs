using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Library.Execution
{
    public static class ParameterArguments
    {
        public static List<string> Build(Dictionary<string, string> parameters)
        {
            if (parameters == null)
            {
                return new List<string>();
            }
            return parameters
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter => parameter.Key + "=" + parameter.Value)
                .ToList();
        }
    }
}
