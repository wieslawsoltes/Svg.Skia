using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework("Avalonia.Svg.Skia.UiTests.AvaloniaUiTestFramework", "Avalonia.Svg.Skia.UiTests")]
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true, MaxParallelThreads = 1)]
namespace Avalonia.Svg.Skia.UiTests
{

    public class AvaloniaUiTestFramework : XunitTestFramework
    {
        public AvaloniaUiTestFramework(IMessageSink messageSink)
            : base(messageSink)
        {
        }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
            => new Executor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);

        private class Executor : XunitTestFrameworkExecutor
        {
            public Executor(
                AssemblyName assemblyName,
                ISourceInformationProvider sourceInformationProvider,
                IMessageSink diagnosticMessageSink)
                : base(
                    assemblyName,
                    sourceInformationProvider,
                    diagnosticMessageSink)
            {

            }

            protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases,
                IMessageSink executionMessageSink,
                ITestFrameworkExecutionOptions executionOptions)
            {
                executionOptions.SetValue("xunit.execution.DisableParallelization", false);
                using var assemblyRunner = new Runner(
                    TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink,
                    executionOptions);

                await assemblyRunner.RunAsync();
            }
        }

        private class Runner : XunitTestAssemblyRunner
        {
            public Runner(
                ITestAssembly testAssembly,
                IEnumerable<IXunitTestCase> testCases,
                IMessageSink diagnosticMessageSink,
                IMessageSink executionMessageSink,
                ITestFrameworkExecutionOptions executionOptions)
                : base(
                    testAssembly,
                    testCases,
                    diagnosticMessageSink,
                    executionMessageSink,
                    executionOptions)
            {

            }

            public override void Dispose()
            {
                AvaloniaApp.Stop();

                base.Dispose();
            }

            protected override void SetupSyncContext(int maxParallelThreads)
            {
                var tcs = new TaskCompletionSource<SynchronizationContext>();
                var thread = new Thread(() =>
                {
                    try
                    {
                        //AvaloniaApp.RegisterDependencies();

                        AvaloniaApp
                            .BuildAvaloniaApp()
                            .AfterSetup(_ =>
                            {
                                tcs.SetResult(SynchronizationContext.Current!);
                            })
                            .StartWithClassicDesktopLifetime(new string[0]);
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                })
                {
                    IsBackground = true
                };

                thread.Start();

                SynchronizationContext.SetSynchronizationContext(tcs.Task.Result);
            }
        }
    }
}
