# 0007. Catalog refers to Administration's MediaType by id only

- Status: Accepted
- Date: 2026-09-30
- Phase: 2 (see `docs/upgrade-plan.md`)

## Context

`catalog.Track.MediaTypeId` points at `administration.MediaType`. Until phase
2 it was a foreign key with a `Track.MediaType` navigation and a
`MediaType.Tracks` inverse, and track responses carried `MediaTypeName`.

## Decision

- Drop the navigation, its inverse and the foreign key
  `FK_Track_MediaType_MediaTypeId`. Keep `MediaTypeId` as an indexed column;
  `GET /api/catalog/tracks/mediatype/{id}` filters on it.
- Track responses carry `MediaTypeId` only; `MediaTypeName` and the nested
  media type are gone.
- Names, if needed again, come from a read contract in
  `Administration.Contracts` (phase 3).

## Consistency

Application-validated id, no database foreign key, eventual. Today `TrackValidator` requires a non-null `MediaTypeId`; nothing checks that
the media type exists.

## Consequences

- A track can name a media type id that does not exist. There is no media
  type delete endpoint today, so the realistic way to orphan a track is a
  direct database change; if a delete endpoint is added it behaves like genre
  deletion in ADR-0006.
- A missing media type leaves tracks with a dangling `MediaTypeId`; phase 6's
  orphan check reports them.

## How to reverse

Re-add the navigation and foreign key in a context that maps both (undoing
ADR-0003 for Catalog and Administration); remove or reassign orphaned tracks
first.
