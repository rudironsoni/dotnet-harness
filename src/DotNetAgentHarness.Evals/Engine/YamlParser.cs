using System;
using System.Collections.Generic;
using System.IO;
using DotNetAgentHarness.Evals.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DotNetAgentHarness.Evals.Engine;

public static class YamlParser
{
    public static List<EvalCase> LoadCases(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Evaluation cases file not found: {filePath}");

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var yaml = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new InvalidDataException($"Evaluation cases file '{filePath}' is empty.");
        }

        var cases = deserializer.Deserialize<List<EvalCase>>(yaml);
        if (cases is null)
        {
            throw new InvalidDataException($"Evaluation cases file '{filePath}' could not be deserialized into a case list.");
        }

        if (cases.Count == 0)
        {
            throw new InvalidDataException($"Evaluation cases file '{filePath}' did not contain any eval cases.");
        }

        // Fail fast schema validation
        foreach (var c in cases)
        {
            if (c is null)
            {
                throw new InvalidDataException($"Evaluation cases file '{filePath}' contains a null case entry.");
            }

            if (string.IsNullOrWhiteSpace(c.Id)) throw new InvalidDataException("EvalCase missing 'id'");
            if (string.IsNullOrWhiteSpace(c.Prompt)) throw new InvalidDataException($"EvalCase {c.Id} missing 'prompt'");
            if (c.TrialCount is <= 0)
            {
                throw new InvalidDataException($"EvalCase {c.Id} has invalid 'trial_count'. Use a value >= 1.");
            }

            if (c.Assertions is null)
            {
                throw new InvalidDataException($"EvalCase {c.Id} missing 'assertions'.");
            }

            foreach (var assertion in c.Assertions)
            {
                if (string.IsNullOrWhiteSpace(assertion.Type))
                {
                    throw new InvalidDataException($"EvalCase {c.Id} contains assertion with missing 'type'.");
                }
            }
        }

        return cases;
    }
}
