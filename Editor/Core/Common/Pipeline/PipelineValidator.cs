using System;
using System.Collections.Generic;
using System.Linq;

namespace SmearFramework
{
    public static class PipelineValidator
    {
        // run all checks, return one report
        public static ValidationReport Validate(
            IReadOnlyList<IPipelineStage> stages,
            IReadOnlyList<ArtifactKey> preloadedKeys)
        {
            var issues = new List<ValidationIssue>();

            if (stages == null || stages.Count == 0)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                    "Pipeline is empty -- add at least one stage."));
                return new ValidationReport(issues);
            }

            CheckInputs(stages, preloadedKeys, issues);
            CheckDuplicateProducers(stages, issues);
            CheckOrphanOutputs(stages, issues);

            return new ValidationReport(issues);
        }

        // walk stages top-down; every InputKey must be preloaded or produced by an earlier stage
        private static void CheckInputs(
            IReadOnlyList<IPipelineStage> stages,
            IReadOnlyList<ArtifactKey> preloadedKeys,
            List<ValidationIssue> issues)
        {
            var availableTypes = new Dictionary<string, Type>();
            if (preloadedKeys != null)
            {
                foreach (var k in preloadedKeys)
                    availableTypes[k.Key] = k.Type;
            }

            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                CheckStageInputs(stage, availableTypes, issues);

                foreach (var output in stage.OutputKey)
                    availableTypes[output.Key] = output.Type;
            }
        }

        // per-stage helper so the outer loop stays two levels deep
        private static void CheckStageInputs(
            IPipelineStage stage,
            Dictionary<string, Type> availableTypes,
            List<ValidationIssue> issues)
        {
            foreach (var input in stage.InputKey)
            {
                if (!availableTypes.TryGetValue(input.Key, out var producedType))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error,
                        $"Stage '{stage.Name}' needs '{input.Key}' but no earlier stage produces it."));
                    continue;
                }
                if (producedType != input.Type)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error,
                        $"'{input.Key}' has type mismatch for '{stage.Name}': produced as '{producedType.Name}', consumed as '{input.Type.Name}'."));
                }
            }
        }

        // two stages both declaring the same OutputKey is an error
        private static void CheckDuplicateProducers(
            IReadOnlyList<IPipelineStage> stages,
            List<ValidationIssue> issues)
        {
            var seen = new Dictionary<string, string>(); // key -> first producer name
            foreach (var stage in stages)
            {
                foreach (var output in stage.OutputKey)
                {
                    if (seen.TryGetValue(output.Key, out var firstName))
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Error,
                            $"'{output.Key}' is produced by both '{firstName}' and '{stage.Name}'. Duplicate producer."));
                    }
                    else
                    {
                        seen[output.Key] = stage.Name;
                    }
                }
            }
        }

        // an artifact nothing downstream consumes is a warning (probably a typo or stale output)
        private static void CheckOrphanOutputs(
            IReadOnlyList<IPipelineStage> stages,
            List<ValidationIssue> issues)
        {
            var allInputs = stages.SelectMany(s => s.InputKey.Select(k => k.Key)).ToHashSet();

            foreach (var stage in stages)
            {
                foreach (var output in stage.OutputKey)
                {
                    if (!allInputs.Contains(output.Key))
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                            $"orphan: '{output.Key}' produced by '{stage.Name}' but no stage consumes it."));
                    }
                }
            }
        }
    }
}
