# Security policy

## Supported versions

ResulTus follows Semantic Versioning. Security fixes land on the latest released minor line; older
lines receive a fix only when the owner decides a backport is warranted.

| Version | Supported |
| ------- | --------- |
| 1.2.x   | yes       |
| < 1.2   | no        |

## Reporting a vulnerability

Do not open a public issue, and do not put exploit details in a pull request. Report privately
through GitHub's private vulnerability reporting: **Security → Advisories → Report a vulnerability**.
The report is visible only to the maintainers.

A useful report states:

- the package and version (`ResulTus`, `ResulTus.HttpClient`, …),
- the target framework and runtime,
- a minimal reproduction,
- the impact you believe it has,
- any mitigation you already found.

## What to expect

Reports are acknowledged as soon as the owner sees them. A confirmed issue gets a fix, a regression
test that fails before it, and a release note in `CHANGELOG.md` that describes the impact without
publishing the exploit. Credit is given in the advisory unless you ask otherwise.

## Scope

In scope: the published packages under `src/`, the analyzers and source generator, and the
serialization paths, including trimming and Native AOT behavior.

Out of scope: vulnerabilities in a framework or a third-party dependency (report them upstream),
and requests that would make the library impose an opinionated policy it deliberately does not have.
