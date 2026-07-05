// Copyright (c) ResulTus. All rights reserved.

using BenchmarkDotNet.Running;
using ResulTus.Benchmarks;

BenchmarkRunner.Run<ResultCreationBenchmark>();
BenchmarkRunner.Run<ResultMatchBenchmark>();
BenchmarkRunner.Run<AllocationBenchmark>();
