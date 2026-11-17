// Copyright (c) ResulTus. All rights reserved.

// The documentation snippet gate. Every ```csharp block in the user-facing documentation is
// compiled against the real source projects, so a snippet that drifted from the shipped API
// fails a check instead of a review. Usage: dotnet run --project scripts/SnippetGate

using SnippetGate;

return SnippetGateRunner.Run();
