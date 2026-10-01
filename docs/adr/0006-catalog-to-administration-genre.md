# 0006. Catalog refers to Administration's Genre by id only

- Status: Accepted
- Date: 2026-09-30
- Phase: 2 (see `docs/upgrade-plan.md`)

## Context

`catalog.Track.GenreId` points at `administration.Genre`. Until phase 2 it
was a foreign key with a `Track.Genre` navigation and a `Genre.Tracks`
inverse, and track responses (directly, and nested in albums, artists and
playlists) carried `GenreName`. Genre is the one entity with write endpoints,
including `DELETE /api/admin/genres/{id}`.

## Decision

- Drop the navigation, its inverse and the foreign key
  `FK_Track_Genre_GenreId`. Keep `GenreId` as an indexed column; `GET
  /api/catalog/tracks/genre/{id}` filters on it.
- Track responses carry `GenreId` only; `GenreName` and the nested genre are
  gone.
- Names, if needed again, come from a genre read contract in
  `Administration.Contracts` (phase 3).

## Consistency

Application-validated id, no database foreign key, eventual. Today `TrackValidator` requires a non-null `GenreId`; nothing checks that the
genre exists.

## Consequences

- Behaviour change: `DELETE /api/admin/genres/{id}` on a genre that tracks
  still use now succeeds. Before, the foreign key rejected it and the request
  failed with a 500. After the delete those tracks keep a `GenreId` that
  refers to nothing, and `/api/catalog/tracks/genre/{id}` still returns them.
- Phase 6's orphan check reports tracks whose genre is gone. If that turns out
  to be common, Administration can refuse to delete a genre in use by asking
  Catalog through a contract, or Catalog can react to a genre-deleted event.

## How to reverse

Re-add the navigation and foreign key in a context that maps both (undoing
ADR-0003 for Catalog and Administration); remove or reassign orphaned tracks
first.
