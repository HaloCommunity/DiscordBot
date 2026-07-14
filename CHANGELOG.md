# Changelog

All notable changes to HaloCommunityBot will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.7.3] - 2026-07-14

### Changed

* support user ID and mention parsing for ban and unban commands

## [1.7.2] - 2026-07-04

### Changed

* Fix /warnings clear component custom-ID mismatch causing silent interaction failure

## [1.7.1] - 2026-07-04

### Changed

* Fix silent component-command failures and gateway-blocking MessageReceived handlers

## [1.7.0] - 2026-07-04

### Added

* Add warning removal: /warnings clear with interactive picker for individual or bulk removal

## [1.6.1] - 2026-07-04

### Changed

* Fix mod-log thread lookup limit and /warnings ordering

## [1.6.0] - 2026-07-04

### Added

* Add per-user mod-log thread reuse, durable warning persistence, and all-caps auto-moderation

## [1.5.2] - 2026-06-30

### Fixed

* Link member name to Discord profile in join audit embed

## [1.5.1] - 2026-06-26

### Fixed

* Show username and profile image in member join and leave audit embeds

## [1.5.0] - 2026-06-25

### Added

* Add member join logging and per-user ignore list for event audit

## [1.4.21] - 2026-06-25

### Changed

* Add move messages and thread relocation with reaction and pinned metadata copy

## [1.4.20] - 2026-06-24

### Changed

* Restore clickable deleted-message author mention and author icon rendering

## [1.4.19] - 2026-06-24

### Changed

* Guarantee deleted-message avatar thumbnail fallback

## [1.4.18] - 2026-06-24

### Changed

* Stabilize deleted message attribution with early capture and author profile fallback

## [1.4.17] - 2026-06-24

### Changed

* Add receive-time snapshot fallback for deleted message author attribution

## [1.4.16] - 2026-06-24

### Changed

* Enable message cache for deleted-message author attribution

## [1.4.15] - 2026-06-24

### Changed

* Improve deleted message author attribution from audit logs

## [1.4.14] - 2026-06-24

### Changed

* Add moderation event audit logging and deploy secret wiring

## [1.4.13] - 2026-06-20

### Changed

* show offending image in cross-channel spam log embed

## [1.4.12] - 2026-06-18

### Changed

* Show username and user ID in /singlemessage list output

## [1.4.11] - 2026-06-18

### Changed

* Add EF migration for persistent single-message backfill startup fix

## [1.4.10] - 2026-06-18

### Changed

* Add persistent background single-message history backfill

## [1.4.9] - 2026-06-18

### Changed

* Fix single-message enable interaction timeout by deferring response

## [1.4.8] - 2026-06-15

### Changed

* Add configurable cross-channel spam enforcement defaults (delete+timeout on)

## [1.4.7] - 2026-06-15

### Changed

* Fix live test detection race via TCS; make content optional (attachment-only test now supported)

## [1.4.6] - 2026-06-15

### Changed

* Fix cross-channel live test detection state and add attachment-aware spam test support

## [1.4.5] - 2026-06-15

### Changed

* Improve cross-channel spam detection fingerprinting, logging, and add cleanup-enabled live testing

## [1.4.4] - 2026-06-15

### Changed

* Add moderation exemptions, command access controls, and forum log resolution fallback

## [1.4.3] - 2026-06-15

### Changed

* `SingleMessageService` is now fully DB-backed — channel registration no longer requires an `appsettings.json`/env-var config entry; `/singlemessage enable` and `/singlemessage disable` operate directly on the database at runtime with no redeploy needed
* `/singlemessage enable` gains a `scan_history` parameter (default `true`) replacing the old per-channel config flag
* `/singlemessage list` now shows enforcement status (active / disabled) alongside posted users

### Added

* `/spam test` command (requires Manage Messages) — dry-runs the cross-channel spam detector against any text, showing the computed fingerprint, current config, trigger conditions, and enforcement actions without taking any real action

## [1.4.2] - 2026-06-15

### Fixed

* `YoutubeFeedUrlsEndpointHostedService` is no longer registered when the YouTube monitor is disabled — previously it started unconditionally and would crash with `ObjectDisposedException` at startup

## [1.4.1] - 2026-06-15

### Fixed

* YouTube forum post title now guards against orphaned surrogate pairs at the 100-character truncation boundary
* YouTube forum post title falls back to `[{ChannelName}] {VideoId}` if template substitution produces an empty or whitespace-only result
* Log the resolved post title (with length) at Info level before posting, to aid diagnosis of future `BASE_TYPE_BAD_LENGTH` rejections

## [1.4.0] - 2026-06-14

### Added

* Moderation action logging: all mod actions (ban, unban, kick, mute, unmute, warn, clear, purge, lock, unlock, slowmode, single-message deletions) post rich embeds to a configurable forum channel
* Cross-channel spam detection: detects identical messages across channels within a time window, with moderator ban/dismiss action buttons
* YouTube title template expanded placeholder set: `{VideoId}`, `{VideoUrl}`, `{ChannelId}`, `{PublishedDate}`, `{PublishedAtUtc}`, `{PublishedAtDiscord}`, `{PublishedAtDiscordRelative}`, `{VideoDescription}`, `{RoleMention}`, and escaped newline support

## [1.3.0] - 2026-06-14

### Added

* Add single-message-per-user channel enforcement with /singlemessage slash commands

## [1.2.0] - 2026-06-09

### Added

* YouTube post title truncation, status monitor reply improvements, and cooldown configuration

## [1.1.52] - 2026-06-01

### Changed

* Fix EF migration discovery for Halo status incident thread mapping

## [1.1.51] - 2026-06-01

### Changed

* Improve Halo status timeliness and reply threading

## [1.1.50] - 2026-05-27

### Changed

* Switch YouTube feed endpoint to WebApplication

## [1.1.49] - 2026-05-27

### Changed

* Improve slash command registration diagnostics

## [1.1.48] - 2026-05-27

### Changed

* Expose and emit endpoint-based YouTube observability metrics

## [1.1.47] - 2026-05-27

### Changed

* Add YouTube feed URL endpoint for observability

## [1.1.46] - 2026-05-27

### Changed

* Add Prometheus metrics endpoint

## [1.1.45] - 2026-05-21

### Changed

* Add observed-feed logging for reconciliation dashboard

## [1.1.44] - 2026-05-19

### Changed

* Version bump

## [1.1.43] - 2026-05-19

### Changed

* Downgrade disconnect lifecycle warnings to informational logging

## [1.1.42] - 2026-05-19

### Changed

* Harmonize reconnect-safe slash command registration and transient disconnect logging

## [1.1.41] - 2026-05-19

### Changed

* Conditionally register YouTube monitor service and sync poll interval from config

## [1.1.40] - 2026-05-19

### Changed

* Register slash commands only once; skip re-registration on gateway reconnects

## [1.1.39] - 2026-05-19

### Changed

* Downgrade graceful Discord disconnect log from Warning to Information

## [1.1.38] - 2026-05-19

### Changed

* Fix missing FeedPostStates table by switching database initialisation from EnsureCreated to MigrateAsync

## [1.1.37] - 2026-05-19

### Changed

* Fix heartbeat monitor not starting when slash command registration fails on startup

## [1.1.36] - 2026-05-18

### Changed

* Use uploads playlist polling for YouTube monitor to reduce API quota usage

## [1.1.35] - 2026-05-18

### Changed

* Adjust Discord gateway log level and clamp YouTube poll interval

## [1.1.34] - 2026-05-18

### Changed

* Switch YouTube monitoring to Data API with quota backoff

## [1.1.33] - 2026-05-14

### Changed

* Make YouTube settings queries deterministic to remove EF warning

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
