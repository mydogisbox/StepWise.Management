---
name: Do not read Walkthrough source files
description: User has explicitly forbidden reading Walkthrough source code files
type: feedback
---

Never read files under `../Walkthrough` or the Walkthrough source directory. Treat Walkthrough as a black-box NuGet package. Use only the CLAUDE.md docs at `tests/StepWise.Management.UI.Tests/.claude/walkthrough/` and the visible usage patterns in the test files.

**Why:** User has explicitly stated this is off-limits, consistent with CLAUDE.md treating it as a read-only dependency.

**How to apply:** If you need to understand the Walkthrough API, look at existing usage in test files or consult the walkthrough CLAUDE.md. Do not grep or read the Walkthrough repo source.
