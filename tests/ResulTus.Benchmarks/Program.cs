// Copyright (c) ResulTus. All rights reserved.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.ConsoleArguments;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using ResulTus.Benchmarks;

// The command line is parsed into filters and applied on top of DefaultConfig, so that
// `--filter` keeps working for the explicit Run<T>() calls below. ConfigParser on its own
// returns a sparse config with no logger and no column providers, which would print nothing.
// A rejected option makes it return no config at all after printing the error and the usage,
// so the null check turns what used to be a NullReferenceException into a plain non-zero exit.
var parsed = ConfigParser.Parse(args, ConsoleLogger.Default);
if (parsed.config is null)
{
    Console.Error.WriteLine("BenchmarkDotNet rejected the command line (see the error and usage above); nothing was run.");
    Environment.Exit(1);
}

var config = ManualConfig.Create(DefaultConfig.Instance)
    .AddFilter(parsed.config.GetFilters().ToArray());

BenchmarkRunner.Run<ResultCreationBenchmark>(config);
BenchmarkRunner.Run<ResultMatchBenchmark>(config);
BenchmarkRunner.Run<AllocationBenchmark>(config);
BenchmarkRunner.Run<FailurePathAllocationBenchmark>(config);
BenchmarkRunner.Run<CollectionOperationBenchmark>(config);
BenchmarkRunner.Run<SerializationOperationBenchmark>(config);
ArrayTraversalBenchmark.PrintLayoutTable();
BenchmarkRunner.Run<ArrayTraversalBenchmark>(config);
BenchmarkRunner.Run<GenericTierTraversalBenchmark>(config);
