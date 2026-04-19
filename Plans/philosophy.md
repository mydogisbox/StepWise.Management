# Test philosophy

Each test is a specification of intent, not a script. It states what matters — the names,
ids, and values that are meaningful to the scenario — and lets defaults handle everything
else. A test that asserts on four fields only mentions four fields; the surrounding
infrastructure is invisible.

A few principles follow from that:

**Defaults carry the contract.** Every step definition encodes the correct way to use that
step. Tests override only what they need to distinguish or assert on. If a default is wrong,
every test that relies on it breaks — which is the right failure mode.

**No static strings in defaults.** Generated guids prevent tests from accidentally passing
because two tests share a hardcoded name. Each run is isolated by construction.

**Round-trip verification over capture reuse.** Cross-aggregate references go through the
GET endpoint rather than reusing the value from the request that created them. This forces
each test to verify that data was actually persisted and retrievable, not just that it was
sent.

**Accumulation separates concerns.** Build steps accumulate commands without sending them.
A single post step dispatches the whole batch. This mirrors the domain model — commands are
grouped by aggregate — and keeps the test readable as a sequence of intent rather than a
sequence of HTTP calls.

**Shared workflows for setup, not for assertions.** Common setup is extracted once and
embedded by reference. The shared workflow carries no assertions of its own; it exists only
to put the system in the right state for the test that matters.

The result is tests that read like requirements: create a target, add a step, archive it,
verify it is archived. The plumbing disappears.
