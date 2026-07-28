# Support Policy

## Release Channels

GMA Framework is independently versioned. The `dev` branch is the changing integration line; immutable SemVer tags are release boundaries.

| Channel | Status |
| --- | --- |
| `dev` | Pre-release integration |
| `v0.2.0` | Current tagged release |

## Compatibility

A release contains the owned repository source archive, release manifest, checksums, CycloneDX SBOM, and GitHub attestations.

Pre-1.0 releases may contain breaking changes between minor versions. Compatibility promises belong to each repository and its tagged release notes; composition repositories do not replace those contracts.

## End Of Life

Only `dev` and the current tagged release receive fixes during the pre-1.0 period. Older tags are end of life when a newer tag is published unless a release note explicitly states otherwise.

## Support

Security reports follow `SECURITY.md`. Maintenance is best effort and has no contractual support SLA.
