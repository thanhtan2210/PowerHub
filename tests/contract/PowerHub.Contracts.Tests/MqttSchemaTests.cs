using System.Text.Json;
using Json.Schema;

namespace PowerHub.Contracts.Tests;

/// <summary>
/// Keeps the published MQTT schemas honest: every documented valid payload must pass and
/// every documented invalid payload must fail, so a schema edit cannot silently loosen or
/// break the device contract.
/// </summary>
public sealed class MqttSchemaTests
{
    private static readonly string Root = Path.Combine(RepositoryRoot(), "contracts", "mqtt");

    public static TheoryData<string> Examples(string kind) => [.. FileNames(kind)];

    [Theory]
    [MemberData(nameof(Examples), "valid")]
    public void Valid_example_is_accepted(string file) =>
        Assert.True(Evaluate("valid", file), $"{file} should satisfy its schema.");

    [Theory]
    [MemberData(nameof(Examples), "invalid")]
    public void Invalid_example_is_rejected(string file) =>
        Assert.False(Evaluate("invalid", file), $"{file} should be rejected by its schema.");

    [Fact]
    public void Every_schema_has_valid_and_invalid_examples()
    {
        foreach (var schema in Directory.GetFiles(Root, "*.v1.schema.json"))
        {
            var name = Path.GetFileName(schema).Replace(".v1.schema.json", "", StringComparison.Ordinal);
            Assert.Contains(FileNames("valid"), file => file.StartsWith(name + ".", StringComparison.Ordinal));
            Assert.Contains(FileNames("invalid"), file => file.StartsWith(name + ".", StringComparison.Ordinal));
        }
    }

    private static string[] FileNames(string kind) =>
        [.. Directory.GetFiles(Path.Combine(Root, "examples", kind), "*.json").Select(path => Path.GetFileName(path)).Order()];

    private static bool Evaluate(string kind, string file)
    {
        // The part of the file name before the first dot names the schema.
        var schema = Schemas.Value[file[..file.IndexOf('.', StringComparison.Ordinal)]];
        using var payload = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "examples", kind, file)));
        return schema.Evaluate(payload.RootElement).IsValid;
    }

    // Loaded once: each schema declares an $id, which the library registers globally.
    private static readonly Lazy<Dictionary<string, JsonSchema>> Schemas = new(() =>
        Directory.GetFiles(Root, "*.v1.schema.json").ToDictionary(
            path => Path.GetFileName(path).Replace(".v1.schema.json", "", StringComparison.Ordinal),
            path => JsonSchema.FromFile(path)));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PowerHub.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
