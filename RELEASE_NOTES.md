What's New in Luminark v1.1.21:

- Migrated to .NET 10 LTS.
- Fixed idle registry watcher wakeups and ensured settings changes reach the scheduler immediately.
- Serialized monitor commands, bounded pending brightness/volume updates, and improved native handle cleanup and shutdown.
- Prevented delayed monitor discovery from undoing newer brightness changes.
- Added atomic settings saves with backup recovery and configuration validation.
- Removed forced memory trimming and moved logging off input/UI paths.
- Batched rapid tray scroll input and repaired the runtime download/verification path.
- Added dependency locking, pinned release tools, security checks, and regression coverage.