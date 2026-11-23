# Guidelines

A guideline holds rules that a change in this repository must follow and that a reviewer would otherwise enforce from memory. A rule that records a one-time choice belongs in the pull request or an [ADR](../decisions/), not here.

## Structure

Each guideline is one file in this folder and contains:

- **Applies when** — the kind of change or the code shape that triggers the guideline. A reader decides from this statement, not from the subject of the guideline.
- **Rules** — numbered, each one a sentence a reviewer can check against the diff.
- **Evidence** — links to the pull requests and measurements behind each rule, and the versions the measurements ran on.

Index each guideline in [docs/README.md](../README.md) with its applies-when statement and its main rule in one clause.

Write rules in the present tense, without history. A rule that stops holding is removed or rewritten in the pull request that changes the behavior.

## Skills

A skill under `.agents/skills/` can help apply a guideline, for example a procedure for running a measurement. The guideline controls. A skill links to its guideline and does not restate the rules.
