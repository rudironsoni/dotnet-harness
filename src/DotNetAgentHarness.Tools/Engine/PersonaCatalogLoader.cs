using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DotNetAgentHarness.Tools.Engine;

public static class PersonaCatalogLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static PersonaCatalog Load(string repoRoot)
    {
        var personasRoot = Path.Combine(repoRoot, ".rulesync", "personas");
        if (!Directory.Exists(personasRoot))
        {
            throw new DirectoryNotFoundException($"Persona directory not found: {personasRoot}");
        }

        var personas = Directory.EnumerateFiles(personasRoot, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(LoadPersonaFile)
            .ToList();

        if (personas.Count == 0)
        {
            throw new InvalidOperationException($"No persona descriptors were found under {personasRoot}.");
        }

        return new PersonaCatalog
        {
            Personas = personas
        };
    }

    public static PersonaDefinition LoadPersonaFile(string filePath)
    {
        var persona = JsonSerializer.Deserialize<PersonaDefinition>(File.ReadAllText(filePath), JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize persona descriptor '{filePath}'.");

        if (string.IsNullOrWhiteSpace(persona.Id))
        {
            throw new InvalidOperationException($"Persona descriptor '{filePath}' is missing 'id'.");
        }

        if (string.IsNullOrWhiteSpace(persona.DisplayName))
        {
            throw new InvalidOperationException($"Persona descriptor '{filePath}' is missing 'display_name'.");
        }

        if (string.IsNullOrWhiteSpace(persona.Purpose))
        {
            throw new InvalidOperationException($"Persona descriptor '{filePath}' is missing 'purpose'.");
        }

        return persona;
    }
}
