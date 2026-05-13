# Changelog

All notable changes to HaloCommunityBot will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
