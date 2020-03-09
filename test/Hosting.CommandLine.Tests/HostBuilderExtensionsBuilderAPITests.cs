﻿using System;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Conventions;
using McMaster.Extensions.Hosting.CommandLine.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace McMaster.Extensions.Hosting.CommandLine.Tests
{
    public class HostBuilderExtensionsBuilderAPITests
    {
        private readonly ITestOutputHelper _output;

        public HostBuilderExtensionsBuilderAPITests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TestReturnCode()
        {
            var exitCode = await new HostBuilder()
                    .ConfigureServices(collection => collection.AddSingleton<IConsole>(new TestConsole(_output)))
                    .RunCommandLineApplicationFluentAsync(new string[0], (app, provider) => app.OnExecute(() => 42));
            Assert.Equal(42, exitCode);
        }

        [Fact]
        public async Task TestConsoleInjection()
        {
            var console = new Mock<IConsole>();
            var textWriter = new Mock<TextWriter>();
            textWriter.Setup(writer => writer.WriteLine("42")).Verifiable();
            console.SetupGet(c => c.Out).Returns(textWriter.Object);
            await new HostBuilder()
                .ConfigureServices(collection => collection.AddSingleton<IConsole>(console.Object))
                .RunCommandLineApplicationFluentAsync(new string[0], (app, provider) => app.OnExecute(() => app.Out.WriteLine("42")));
            Mock.Verify(console, textWriter);
        }

        [Fact]
        public async Task TestConventionInjection()
        {
            var convention = new Mock<IConvention>();
            convention.Setup(c => c.Apply(It.IsAny<ConventionContext>()))
                .Callback((ConventionContext c) =>
                    c.Application.UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.StopParsingAndCollect)
                .Verifiable();
            var args = new[] { "Capture", "some", "test", "arguments" };
            string[] remainingArgs = null;
            await new HostBuilder()
                .ConfigureServices(collection => collection
                    .AddSingleton<IConsole>(new TestConsole(_output))
                    .AddSingleton(convention.Object))
                .RunCommandLineApplicationFluentAsync(args, (app, provider) => app.OnExecute(() =>
                {
                    remainingArgs = app.RemainingArguments.ToArray();
                }));
            Assert.Equal(args, remainingArgs);
            Mock.Verify(convention);
        }

        [Fact]
        public void ItThrowsOnUnknownSubCommand()
        {
            var ex = Assert.Throws<UnrecognizedCommandParsingException>(
                () => new HostBuilder()
                    .ConfigureServices(collection => collection.AddSingleton<IConsole>(new TestConsole(_output)))
                    .RunCommandLineApplicationFluentAsync(new string[] { "return41" }, (app, provider) =>
                    {
                        app.Command("return42", (cmd => cmd.OnExecute(() => 42)));
                    })
                    .GetAwaiter()
                    .GetResult());
            Assert.Equal(new string[] { "return42" }, ex.NearestMatches);
        }

        [Fact]
        public void ItRethrowsThrownExceptions()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => new HostBuilder()
                    .ConfigureServices(collection => collection.AddSingleton<IConsole>(new TestConsole(_output)))
                    .RunCommandLineApplicationFluentAsync(new string[0], (app, provider) => app.OnExecute(() => throw new InvalidOperationException("A test")))
                    .GetAwaiter()
                    .GetResult());
            Assert.Equal("A test", ex.Message);
        }

        [Fact]
        public void TestUsingServiceProvider()
        {
            IHostEnvironment env = null;

            new HostBuilder()
                .ConfigureServices(collection => collection.AddSingleton<IConsole>(new TestConsole(_output)))
                .RunCommandLineApplicationFluentAsync(new string[0], (app, provider) => app.OnExecute(() =>
                {
                    env = provider.GetRequiredService<IHostEnvironment>();
                }))
                .GetAwaiter()
                .GetResult();

            Assert.NotNull(env);
        }
    }
}
