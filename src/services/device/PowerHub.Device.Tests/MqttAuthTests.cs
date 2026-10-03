using System.Net;
using System.Net.Http.Json;
using PowerHub.Device.Features;

namespace PowerHub.Device.Tests;

public sealed class MqttTopicPolicyTests
{
    private static readonly Guid Device = Guid.Parse("0199aaaa-0000-7000-8000-000000000001");
    private static readonly Guid Other = Guid.Parse("0199aaaa-0000-7000-8000-000000000002");

    private static string Topic(Guid device, string rest) => $"powerhub/v1/devices/{device}/{rest}";

    [Theory]
    [InlineData("telemetry", MqttAccess.Write)]
    [InlineData("state/reported", MqttAccess.Write)]
    [InlineData("commands/cmd-1/result", MqttAccess.Write)]
    [InlineData("state/desired", MqttAccess.Read)]
    [InlineData("state/desired", MqttAccess.Subscribe)]
    [InlineData("commands/cmd-1", MqttAccess.Read)]
    [InlineData("commands/cmd-1", MqttAccess.Subscribe)]
    [InlineData("commands/+", MqttAccess.Subscribe)]
    public void A_device_may_use_its_own_contract_topics(string rest, int access) =>
        Assert.True(MqttTopicPolicy.IsAllowed(Device, Topic(Device, rest), access));

    [Theory]
    // Wrong direction: a device does not read its own telemetry or write its own commands.
    [InlineData("telemetry", MqttAccess.Read)]
    [InlineData("telemetry", MqttAccess.Subscribe)]
    [InlineData("state/reported", MqttAccess.Subscribe)]
    [InlineData("state/desired", MqttAccess.Write)]
    [InlineData("commands/cmd-1", MqttAccess.Write)]
    [InlineData("commands/cmd-1/result", MqttAccess.Subscribe)]
    [InlineData("telemetry", MqttAccess.ReadWrite)]
    // Wildcards beyond the single permitted filter.
    [InlineData("#", MqttAccess.Subscribe)]
    [InlineData("+", MqttAccess.Subscribe)]
    [InlineData("commands/#", MqttAccess.Subscribe)]
    [InlineData("commands/+/result", MqttAccess.Subscribe)]
    [InlineData("commands/+/result", MqttAccess.Write)]
    [InlineData("commands/#/result", MqttAccess.Write)]
    [InlineData("commands/+", MqttAccess.Read)]
    // Topics outside the contract.
    [InlineData("", MqttAccess.Write)]
    [InlineData("telemetry/extra", MqttAccess.Write)]
    [InlineData("commands//result", MqttAccess.Write)]
    [InlineData("anything-else", MqttAccess.Write)]
    [InlineData("telemetry", 0)]
    [InlineData("telemetry", 99)]
    public void Everything_else_on_its_own_prefix_is_denied(string rest, int access) =>
        Assert.False(MqttTopicPolicy.IsAllowed(Device, Topic(Device, rest), access));

    [Theory]
    [InlineData(MqttAccess.Write, "telemetry")]
    [InlineData(MqttAccess.Subscribe, "state/desired")]
    [InlineData(MqttAccess.Subscribe, "commands/+")]
    [InlineData(MqttAccess.Read, "commands/cmd-1")]
    public void Another_devices_topics_are_denied(int access, string rest) =>
        Assert.False(MqttTopicPolicy.IsAllowed(Device, Topic(Other, rest), access));

    [Theory]
    [InlineData("#")]
    [InlineData("powerhub/#")]
    [InlineData("powerhub/v1/devices/#")]
    [InlineData("powerhub/v1/devices/+/telemetry")]
    [InlineData("powerhub/v1/devices/+/commands/+")]
    [InlineData("$SYS/#")]
    [InlineData("powerhub/v2/devices/0199aaaa-0000-7000-8000-000000000001/telemetry")]
    [InlineData("POWERHUB/V1/DEVICES/0199AAAA-0000-7000-8000-000000000001/telemetry")]
    [InlineData("/powerhub/v1/devices/0199aaaa-0000-7000-8000-000000000001/telemetry")]
    [InlineData(null)]
    public void Topics_outside_the_device_prefix_are_denied(string? topic)
    {
        Assert.False(MqttTopicPolicy.IsAllowed(Device, topic, MqttAccess.Subscribe));
        Assert.False(MqttTopicPolicy.IsAllowed(Device, topic, MqttAccess.Write));
    }
}

public sealed class MqttAuthTests(DeviceApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_issued_credential_authenticates_its_own_device_only()
    {
        using var owner = app.ClientFor(Guid.NewGuid());
        using var broker = app.CreateClient(); // The broker has no user token.
        var first = await RegisterAsync(owner);
        var second = await RegisterAsync(owner);

        Assert.Equal(HttpStatusCode.OK, await AuthAsync(broker, first.Device.Id.ToString(), first.Credential));
        Assert.Equal(HttpStatusCode.Unauthorized, await AuthAsync(broker, second.Device.Id.ToString(), first.Credential));
        Assert.Equal(HttpStatusCode.Unauthorized, await AuthAsync(broker, first.Device.Id.ToString(), first.Credential + "x"));
    }

    [Theory]
    [InlineData("not-a-guid", "secret")]
    [InlineData("", "secret")]
    [InlineData(null, "secret")]
    [InlineData("0199aaaa-0000-7000-8000-00000000ffff", "secret")]
    [InlineData("0199aaaa-0000-7000-8000-00000000ffff", "")]
    [InlineData("0199aaaa-0000-7000-8000-00000000ffff", null)]
    [InlineData("' OR 1=1 --", "' OR 1=1 --")]
    public async Task Malformed_or_unknown_identities_are_rejected_without_detail(string? username, string? password)
    {
        using var broker = app.CreateClient();

        var response = await broker.PostAsJsonAsync("/internal/v1/mqtt/auth", new { username, password, clientid = "c" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("device", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_oversized_password_is_rejected()
    {
        using var owner = app.ClientFor(Guid.NewGuid());
        using var broker = app.CreateClient();
        var device = await RegisterAsync(owner);

        Assert.Equal(HttpStatusCode.Unauthorized, await AuthAsync(broker, device.Device.Id.ToString(), new string('a', DeviceSecrets.MaxLength + 1)));
    }

    [Fact]
    public async Task Rotation_invalidates_the_previous_credential()
    {
        using var owner = app.ClientFor(Guid.NewGuid());
        using var broker = app.CreateClient();
        var original = await RegisterAsync(owner);
        var id = original.Device.Id.ToString();

        var rotated = await owner.PostAsync($"/api/v1/devices/{id}/credentials", null, Ct);
        var replacement = (await rotated.Content.ReadFromJsonAsync<DeviceCredentialResponse>(Ct))!.Credential;

        Assert.Equal(HttpStatusCode.Unauthorized, await AuthAsync(broker, id, original.Credential));
        Assert.Equal(HttpStatusCode.OK, await AuthAsync(broker, id, replacement));
    }

    [Fact]
    public async Task A_removed_device_can_neither_connect_nor_use_its_topics()
    {
        using var owner = app.ClientFor(Guid.NewGuid());
        using var broker = app.CreateClient();
        var device = await RegisterAsync(owner);
        var id = device.Device.Id;
        var topic = MqttTopicPolicy.Prefix(id) + "telemetry";
        Assert.Equal(HttpStatusCode.OK, await AclAsync(broker, id.ToString(), topic, MqttAccess.Write));

        (await owner.DeleteAsync($"/api/v1/devices/{id}", Ct)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, await AuthAsync(broker, id.ToString(), device.Credential));
        // An already-connected session loses access as soon as the broker asks again.
        Assert.Equal(HttpStatusCode.Forbidden, await AclAsync(broker, id.ToString(), topic, MqttAccess.Write));
    }

    [Fact]
    public async Task Topic_access_is_limited_to_the_devices_own_contract_topics()
    {
        using var owner = app.ClientFor(Guid.NewGuid());
        using var broker = app.CreateClient();
        var mine = (await RegisterAsync(owner)).Device.Id;
        var theirs = (await RegisterAsync(owner)).Device.Id;
        var username = mine.ToString();

        Assert.Equal(HttpStatusCode.OK, await AclAsync(broker, username, MqttTopicPolicy.Prefix(mine) + "telemetry", MqttAccess.Write));
        Assert.Equal(HttpStatusCode.OK, await AclAsync(broker, username, MqttTopicPolicy.Prefix(mine) + "commands/+", MqttAccess.Subscribe));

        Assert.Equal(HttpStatusCode.Forbidden, await AclAsync(broker, username, MqttTopicPolicy.Prefix(theirs) + "telemetry", MqttAccess.Write));
        Assert.Equal(HttpStatusCode.Forbidden, await AclAsync(broker, username, MqttTopicPolicy.Prefix(theirs) + "commands/+", MqttAccess.Subscribe));
        Assert.Equal(HttpStatusCode.Forbidden, await AclAsync(broker, username, "powerhub/v1/devices/+/telemetry", MqttAccess.Subscribe));
        Assert.Equal(HttpStatusCode.Forbidden, await AclAsync(broker, username, "#", MqttAccess.Subscribe));
        Assert.Equal(HttpStatusCode.Forbidden, await AclAsync(broker, "not-a-guid", MqttTopicPolicy.Prefix(mine) + "telemetry", MqttAccess.Write));
        Assert.Equal(HttpStatusCode.Forbidden, await AclAsync(broker, Guid.NewGuid().ToString(), "anything", MqttAccess.Write));
    }

    [Fact]
    public async Task No_identity_is_a_superuser()
    {
        using var owner = app.ClientFor(Guid.NewGuid());
        using var broker = app.CreateClient();
        var device = await RegisterAsync(owner);

        var response = await broker.PostAsJsonAsync("/internal/v1/mqtt/superuser", new { username = device.Device.Id }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Broker_endpoints_are_not_part_of_the_public_contract()
    {
        var contract = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "contracts", "openapi", "device.json"), Ct);
        Assert.DoesNotContain("/internal/", contract, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PowerHub.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static async Task<DeviceCredentialResponse> RegisterAsync(HttpClient owner)
    {
        var response = await owner.PostAsJsonAsync("/api/v1/devices", new { name = "Meter", kind = "meter" }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceCredentialResponse>(Ct))!;
    }

    private static async Task<HttpStatusCode> AuthAsync(HttpClient broker, string username, string password) =>
        (await broker.PostAsJsonAsync("/internal/v1/mqtt/auth", new { username, password, clientid = "test-client" }, Ct)).StatusCode;

    private static async Task<HttpStatusCode> AclAsync(HttpClient broker, string username, string topic, int acc) =>
        (await broker.PostAsJsonAsync("/internal/v1/mqtt/acl", new { username, clientid = "test-client", topic, acc }, Ct)).StatusCode;
}
