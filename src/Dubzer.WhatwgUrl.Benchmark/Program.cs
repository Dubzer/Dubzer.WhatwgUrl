using System.Reflection;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance
	.AddJob(Job.Default
		.WithRuntime(CoreRuntime.Core10_0)
		.WithId("JIT")
		.AsBaseline())
	.AddJob(Job.Default
		.WithRuntime(NativeAotRuntime.Net10_0)
		// Make the TFM visible during NuGet props import so ILCompiler targets load.
		.WithArguments([
			new MsBuildArgument("/p:TargetFramework=net10.0"),
			new MsBuildArgument("/p:SelfContained=true"),
			new MsBuildArgument("/p:UseAppHost=true")
		])
		.WithId("NativeAOT"));

BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args, config);
