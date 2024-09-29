using System.Reflection;
using BenchmarkDotNet.Running;
using Dubzer.WhatwgUrl.Benchmark;

BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);