using System.IO;
using DotNetAgentHarness.Evals.Engine;
using Xunit;

namespace DotNetAgentHarness.Evals.Tests;

public class YamlParserTests
{
    [Fact]
    public void LoadCases_Throws_WhenYamlDeserializesToNull()
    {
        var path = WriteTempYaml("null");
        try
        {
            var exception = Assert.Throws<InvalidDataException>(() => YamlParser.LoadCases(path));

            Assert.Contains("could not be deserialized", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadCases_Throws_WhenYamlDeserializesToEmptyCaseList()
    {
        var path = WriteTempYaml("[]");
        try
        {
            var exception = Assert.Throws<InvalidDataException>(() => YamlParser.LoadCases(path));

            Assert.Contains("did not contain any eval cases", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempYaml(string contents)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, contents);
        return path;
    }
}
