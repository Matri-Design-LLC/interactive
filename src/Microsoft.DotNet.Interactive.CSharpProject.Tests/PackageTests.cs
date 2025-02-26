// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using FluentAssertions.Extensions;
using System.Threading.Tasks;
using Pocket;
using Xunit;
using Xunit.Abstractions;
using Microsoft.DotNet.Interactive.CSharpProject.Packaging;
using System.Threading;

namespace Microsoft.DotNet.Interactive.CSharpProject.Tests;

public partial class PackageTests : IDisposable
{
    private readonly CompositeDisposable _disposables = new CompositeDisposable();

    public PackageTests(ITestOutputHelper output)
    {
        _disposables.Add(output.SubscribeToPocketLogger());
    }

    public void Dispose() => _disposables.Dispose();

    [Fact]
    public async Task A_package_is_not_initialized_more_than_once()
    {
        var initializer = new TestPackageInitializer(
            "console",
            "MyProject");

        var package = Create.EmptyWorkspace(initializer: initializer);

        await package.CreateWorkspaceForRunAsync();
        await package.CreateWorkspaceForRunAsync();

        initializer.InitializeCount.Should().Be(1);
    }

    [Fact]
    public async Task Package_after_create_actions_are_not_run_more_than_once()
    {
        var afterCreateCallCount = 0;

        var initializer = new PackageInitializer(
            "console",
            "test",
            afterCreate: async _ =>
            {
                await Task.Yield();
                afterCreateCallCount++;
            });

        var package = Create.EmptyWorkspace(initializer: initializer);

        await package.CreateWorkspaceForRunAsync();
        await package.CreateWorkspaceForRunAsync();

        afterCreateCallCount.Should().Be(1);
    }

    [Fact]
    public async Task A_package_copy_is_not_reinitialized_if_the_source_was_already_initialized()
    {
        var initializer = new TestPackageInitializer(
            "console",
            "MyProject");

        var original = Create.EmptyWorkspace(initializer: initializer);

        await original.CreateWorkspaceForLanguageServicesAsync();

        var copy = await PackageUtilities.Copy(original);

        await copy.CreateWorkspaceForLanguageServicesAsync();

        initializer.InitializeCount.Should().Be(1);
    }

    [Fact]
    public async Task When_package_contains_simple_console_app_then_IsAspNet_is_false()
    {
        var package = await Create.ConsoleWorkspaceCopy();

        await package.CreateWorkspaceForLanguageServicesAsync();

        package.IsWebProject.Should().BeFalse();
    }

    [Fact]
    public async Task When_package_contains_simple_console_app_then_entry_point_dll_is_in_the_build_directory()
    {
        var package = Create.EmptyWorkspace(initializer: new PackageInitializer("console", "empty"));

        await package.CreateWorkspaceForRunAsync();

        package.EntryPointAssemblyPath.Exists.Should().BeTrue();

        package.EntryPointAssemblyPath
            .FullName
            .Should()
            .Be(Path.Combine(
                package.Directory.FullName,
                "bin",
                "Debug",
                package.TargetFramework,
                "empty.dll"));
    }

    [Fact]
    public async Task If_a_build_is_in_fly_the_second_one_will_wait_and_do_not_continue()
    {
        var buildEvents = new LogEntryList();
        var buildEventsMessages = new List<string>();
        var package = await Create.ConsoleWorkspaceCopy(isRebuildable: true);
        var barrier = new Barrier(2);
        using (LogEvents.Subscribe(e =>
               {
                   buildEvents.Add(e);
                   buildEventsMessages.Add(e.Evaluate().Message);
                   if (e.Evaluate().Message.StartsWith("Building package "))
                   {
                       barrier.SignalAndWait(30.Seconds());
                   }
               }, searchInAssemblies: 
               new[] {typeof(LogEvents).Assembly,
                   typeof(ICodeRunner).Assembly}))
        {
            await Task.WhenAll(
                Task.Run(() => package.FullBuildAsync()),
                Task.Run(() => package.FullBuildAsync()));
        }

        buildEventsMessages.Should()
            .Contain(e => e.StartsWith("Building package "+package.Name))
            .And
            .Contain(e => e.StartsWith("Skipping build for package "+package.Name));
    }
}