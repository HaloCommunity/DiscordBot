# Changelog

All notable changes to HaloCommunityBot will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.32] - 2026-05-14

### Changed

* Use non-privileged Discord gateway intents by default

## [1.1.31] - 2026-05-14

### Changed

* Suppress noisy HttpClient heartbeat logs

## [1.1.30] - 2026-05-14

### Changed

* Add YouTube monitor role mention support

## [1.1.29] - 2026-05-14

### Changed

* Fix startup crash from slash command CancellationToken parameter

## [1.1.28] - 2026-05-14

### Changed

* Add token env fallback and startup diagnostics

## [1.1.27] - 2026-05-14

### Changed

* Resolve YouTube channel names through Data API search

## [1.1.26] - 2026-05-14

### Changed

* Reject invalid YouTube references before polling feeds

## [1.1.25] - 2026-05-14

### Changed

* Reduce YouTube feed failure skip logging noise

## [1.1.24] - 2026-05-14

### Changed

* Restore recursive deploy preflight ownership to prevent rsync permission errors

## [1.1.23] - 2026-05-14

### Changed

* Fix deploy chmod step to run under sudo

## [1.1.22] - 2026-05-14

### Changed

* Make deploy preflight sudo-compatible and disable rsync timestamp preservation

## [1.1.21] - 2026-05-14

### Changed

* Validate decoded deploy SSH secret is a private key and normalize CRLF

## [1.1.20] - 2026-05-14

### Changed

* Fix preflight chown to recursively change ownership of all deploy subdirectories

## [1.1.19] - 2026-05-14

### Changed

* Skip rsync timestamp preservation to avoid permission errors on host-owned files

## [1.1.18] - 2026-05-14

### Changed

* Harden deploy SSH key parsing for CI secrets

## [1.1.17] - 2026-05-14

### Changed

* Fix rsync group metadata deploy failures

## [1.1.16] - 2026-05-14

### Changed

* Run Halo service as dedicated runtime user

## [1.1.15] - 2026-05-14

### Changed

* Align deploy preflight permissions with Panda workflow

## [1.1.14] - 2026-05-14

### Changed

* Harden Dependabot updates for NuGet and GitHub Actions

## [1.1.13] - 2026-05-14

### Changed

* Redeploy systemd unit during deploy workflow

## [1.1.12] - 2026-05-14

### Changed

* Preserve SQLite database files during deploy sync

## [1.1.11] - 2026-05-14

### Changed

* Add Discord lifecycle observability and readiness hardening

## [1.1.10] - 2026-05-14

### Changed

* Suppress gateway placeholder log noise more robustly

## [1.1.9] - 2026-05-13

### Changed

* Log tracked YouTube channel DB identifier on feed load failure

## [1.1.8] - 2026-05-13

### Changed

* Disable placeholder UC IDs in YouTube tracked channels

## [1.1.7] - 2026-05-13

### Changed

* Disable invalid YouTube tracked channels and normalize references

## [1.1.6] - 2026-05-13

### Changed

* Improve YouTube feed loading for handle/url channel references

## [1.1.5] - 2026-05-13

### Changed

* Suppress empty Discord gateway noise events

## [1.1.4] - 2026-05-13

### Changed

* Send startup and shutdown heartbeat pings for smoother redeploys

## [1.1.3] - 2026-05-13

### Changed

* Filter noisy Discord gateway null log event

## [1.1.2] - 2026-05-12

### Changed

* enhance YouTube keyword filtering to check video descriptions and support hashtags

## [1.1.1] - 2026-05-12

### Changed

* fix Discord.NET null message logging to avoid 'null' appearing in logs

## [1.1.0] - 2026-05-12

### Added

* support @handle format and per-channel keyword filtering for YouTube channels

## [1.0.7] - 2026-05-12

### Added

* add per-channel keyword filtering for youtube videos

## [1.0.6] - 2026-05-12

### Changed

* add config validation for input parameters and secrets

## [1.0.5] - 2026-05-12

### Changed

* default optional deploy env values for typed bot settings

## [1.0.4] - 2026-05-12

### Changed

* default optional deploy env values for youtube and heartbeat settings

## [1.0.3] - 2026-05-12

### Changed

* add configurable uptime heartbeat monitor

## [1.0.2] - 2026-05-12

### Changed

* add SQLite-backed YouTube monitor with forum posting, tag assignment, and slash-based configuration; persist monitor state to prevent reposts on restart

## \[1.0.1] - 2026-04-27

### Changed

* Enhance about command details and include bot version in serverinfo

## \[1.0.0] - 2026-04-27

### Added

* Initial versioned release tracking for HaloCommunityBot.
