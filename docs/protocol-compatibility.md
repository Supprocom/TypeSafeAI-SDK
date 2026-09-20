# Protocol compatibility notes

Last verified: 2026-09-19

This is a clean-room implementation based on public, first-party TypeSafe material. No third-party
SDK source is used.

## Sources

The implementation was checked against:

1. The [live API reference](https://docs.typesafe.ai/api) and TypeSafe's primitive guides for
   [Noul](https://docs.typesafe.ai/primitives/noul),
   [Choice](https://docs.typesafe.ai/primitives/choice), and
   [Score](https://docs.typesafe.ai/primitives/score).
2. The [live OpenAPI document](https://api.typesafe.ai/openapi.json), version `0.2.0` when checked.
3. TypeSafe's official [JavaScript SDK](https://github.com/typesafe-ai/typesafe-sdk-js), version
   `0.6.0` when checked.
4. TypeSafe's official [Python SDK](https://github.com/typesafe-ai/typesafe-sdk-python), version
   `0.7.0` when checked.

The documentation and live HTTP contract take precedence when generated schemas or client type
definitions differ.

## Implemented HTTP surface

| Method | Path | SDK method |
| --- | --- | --- |
| `POST` | `/v1/systemone` | `SystemOneAsync` |
| `GET` | `/v1/models` | `ListModelsAsync` |

Requests use bearer authentication and JSON. The client sends `User-Agent`, `X-TypeSafe-SDK`,
`X-TypeSafe-Runtime`, and `X-TypeSafe-Retry-Count` metadata while preserving
`x-typesafe-request-id` from responses.

The implemented request and response fields are covered by in-memory wire tests. Unknown answer
discriminators are preserved as `UnknownAnswer` with raw JSON instead of making a newly introduced
answer type unreadable.

## Contract decisions

- State must be a JSON string, object, or array.
- Every request must contain at least one named question.
- Noul, Choice, and Score instructions are optional. When present, they must be a JSON string,
  object, or array. Passing `null` through the .NET API omits the wire property, matching the
  official Python SDK and the live OpenAPI contract.
- A Choice has 1–255 named options. Descriptions may be null.
- A Score has 2–10 ordered, non-null level descriptions.
- Instructions, option descriptions, Noul criteria, and score levels can use structured JSON where
  the primitive documentation permits it.
- Score response `legend` and `probabilities` keys are exposed as integers, matching the official
  Python SDK's ergonomic representation.

At the verification date, the live OpenAPI schema, structured-input documentation, and both
first-party clients allowed instructions to be omitted or null, so this SDK accepts that shape. The
generated Score schema allowed one level while the Score guide and first-party clients require at
least two; the SDK enforces the documented 2–10 level limit locally. It also enforces the Choice
guide's 255-option cap.

## Transport parity

The defaults shared by the official clients are retained:

- API root: `https://api.typesafe.ai`
- Model: `jev-latest`
- Per-attempt timeout: 10 seconds
- Retries after the initial attempt: 2
- Retry statuses: 408, 429, and 500–599 (including TypeSafe's documented 529 overload response)
- Initial/max backoff: 500 ms / 5 seconds
- Subtractive jitter: 0.25
- Retry connection failures and per-attempt timeouts
- Prefer `retry-after-ms`, then standard `Retry-After`

The official clients differ on the overall retry budget and whether a server-provided delay is
bounded. This SDK follows the JavaScript client's per-attempt model: it has no implicit total call
budget and honors server delays only up to 60 seconds before falling back to exponential backoff.

The client buffers each response body inside the per-attempt timeout, so an interrupted or stalled
body is treated consistently with a failed HTTP attempt. Caller cancellation is never converted to
a timeout or connection exception.

## Updating the SDK

For an API compatibility update:

1. Compare the live OpenAPI version and paths with the version above.
2. Review changes to the official JavaScript and Python request, response, retry, and error types.
3. Confirm primitive limits in the public guides.
4. Add or update wire-level tests before changing the public .NET types.
5. Build, test, format-check, pack, inspect package metadata, and run a credentialed smoke test when
   release credentials are available.
