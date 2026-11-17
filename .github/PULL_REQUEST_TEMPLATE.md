## What changed

<!-- One paragraph: the problem and the behavior after this change. Link the issue when one exists. -->

## Which packages and target frameworks

<!-- e.g. ResulTus core, net8.0-net11.0; ResulTus.Extensions, net8.0-net11.0 -->

## Checklist

- [ ] `dotnet build ResulTus.slnx` and `dotnet test ResulTus.slnx` are green, with 0 warnings.
- [ ] Tests exercise the observable effect through the public interface, and one covers each rule
      the documentation or the issue states.
- [ ] Public members carry XML documentation, and allocation-sensitive paths keep their measured
      cost.
- [ ] `CHANGELOG.md` records the change; the documentation and both READMEs stay at parity.
- [ ] Every code block added to a document was built against the real sources.
- [ ] The API snapshot was regenerated with `dotnet run --project scripts/ApiSnapshot -- --write`
      and its diff is part of this change when the public surface moved.
- [ ] Benchmark rows were measured only for what this change affects, or not quoted at all.
- [ ] No `Co-Authored-By`, no AI attribution.

## Risk

<!-- Name the risk item from AGENTS.md when one applies, e.g. "item 1: changes stored data".
     Write "none" when no item applies. -->
