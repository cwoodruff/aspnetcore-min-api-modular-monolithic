# Architecture Decision Records

Each file in this folder records one decision: what was decided, why, and how
to undo it. Every cross-module boundary gets a record here before the code
that implements it merges (see `docs/upgrade-plan.md`, working agreement #7).

## Writing a new ADR

1. Copy `0000-template.md` to `NNNN-short-slug.md` using the next free number.
2. Fill in every section. For a cross-module boundary, the Consistency section
   must name exactly one of: "shared transaction", "outbox, eventual", or
   "read only".
3. Add the record to the index below in the same PR as the code it describes.

Records are not edited after they merge. If a decision changes, write a new
record that supersedes the old one and update the old one's Status line.

## Index

| ADR | Title | Status |
|---|---|---|
| [0001](0001-module-map-and-ownership.md) | Module map and entity ownership | Accepted |
