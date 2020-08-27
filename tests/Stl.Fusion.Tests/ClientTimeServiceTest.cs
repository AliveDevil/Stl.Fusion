using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Stl.Fusion.Tests.Services;
using Stl.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Stl.Fusion.Tests
{
    [Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
    public class ClientTimeServiceTest : FusionTestBase
    {
        public ClientTimeServiceTest(ITestOutputHelper @out, FusionTestOptions? options = null) : base(@out, options) { }

        [Fact]
        public async Task Test1()
        {
            var epsilon = TimeSpan.FromSeconds(0.5);

            await using var serving = await WebSocketHost.ServeAsync();
            var client = Services.GetRequiredService<IClientTimeService>();
            var cTime = await Computed.CaptureAsync(_ => client.GetTimeAsync());
            cTime.IsConsistent().Should().BeTrue();
            (DateTime.Now - cTime.Value).Should().BeLessThan(epsilon);

            await Task.Delay(TimeSpan.FromSeconds(2));
            cTime.IsConsistent().Should().BeFalse();
            var time = await cTime.UseAsync();
            (DateTime.Now - time).Should().BeLessThan(epsilon);
        }

        [Fact]
        public async Task Test2()
        {
            var epsilon = TimeSpan.FromSeconds(0.5);

            await using var serving = await WebSocketHost.ServeAsync();
            var service = Services.GetRequiredService<IClientTimeService>();

            for (int i = 0; i < 20; i++) {
                var time = await service.GetTimeAsync();
                (DateTime.Now - time).Should().BeLessThan(epsilon);
                await Task.Delay(TimeSpan.FromSeconds(0.1));
            }
        }
    }
}
