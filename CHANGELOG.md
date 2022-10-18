# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `AuthenticationTokenHandler` will always include a token in the outgoing requests, except when an anonymous authorization header is already set on the request.
For example, with Refit : `[Headers("Authorization: Anonymous")]`
would indicate to the `AuthenticationTokenHandler` that it doesn't need to include a token for this specific request.

### Changed

### Deprecated

### Removed

### Fixed

### Security
