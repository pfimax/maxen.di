using System;
using System.Collections.Generic;
using System.Linq;

namespace Maxen.DI.Generators
{
    /// <summary>
    /// Finds singletons that take a scoped service in their constructor. The scoped instance would be
    /// captured by the singleton and outlive its scope, which the runtime also refuses; catching it at
    /// compile time saves waiting for the first request to find out.
    /// </summary>
    internal static class CaptiveDependencyChecker
    {
        public static IEnumerable<DiagnosticModel> Check(IEnumerable<ServiceModel> models)
        {
            var services = models.Where(m => m.Info is not null).OrderBy(m => m.SortKey, StringComparer.Ordinal)
                .Select(m => m.Info!).ToList();

            // The last registration of a service wins at runtime, so that is the one a dependency gets.
            var winners = new Dictionary<string, ServiceInfo>();
            foreach (var service in services)
                winners[service.ServiceName + "|" + (service.Key ?? "")] = service;

            foreach (var singleton in services.Where(s => s.Lifetime == "Singleton"))
            {
                foreach (var dependency in singleton.Dependencies.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (winners.TryGetValue(dependency, out var target) && target.Lifetime == "Scoped")
                    {
                        yield return new DiagnosticModel(
                            DiagnosticDescriptors.CaptiveDependency,
                            singleton.Location,
                            Short(singleton.ClassName) + "|" + Short(target.ClassName));
                    }
                }
            }
        }

        /// <summary>Drops the "global::" prefix so messages read naturally.</summary>
        private static string Short(string name) => name.StartsWith("global::", StringComparison.Ordinal) ? name.Substring(8) : name;
    }
}
