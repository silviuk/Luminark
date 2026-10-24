# Luminark Privacy Policy

Last updated: October 24, 2026

This policy applies to Luminark, the Windows display and theme utility published by Silviu Vlasceanu at https://github.com/silviuk/Luminark, including version 1.1.0 and subsequent releases.

## Information used on your device

Luminark processes information locally to provide the features you enable:

- Display information, including monitor descriptions, hardware-stable display identifiers and supported controls, is read through Windows display APIs, WMI and DDC/CI. Custom monitor names are saved against these identifiers so that names remain associated with the same monitor after reconnecting or restarting. Later releases also save custom input names, visible input choices and known monitor identifiers.
- Preferences include theme and brightness schedules, day/night times and brightness levels, Night Light synchronization, monitor linking, keyboard shortcuts, startup and tray behavior, input-switch and lock-screen delays, and sleep-prevention options and durations. These settings are used to remember and apply your choices.
- Windows theme, Night Light state and schedule information, display changes, and power and session events are read to coordinate theme, brightness and sleep behavior. Sunrise and sunset times supplied by Windows Night Light may be used; Luminark does not request GPS coordinates or directly access Windows location services.
- Diagnostic logs record timestamps, application events and errors. Depending on the release and event, they can include monitor names or identifiers, capabilities, brightness or input values, shortcut settings, and file paths or exception details. These logs support troubleshooting and are not automatically uploaded.

## Storage, retention and control

Settings are stored as readable JSON in `%LOCALAPPDATA%\Luminark\settings.json`. Diagnostic logs are written to `%LOCALAPPDATA%\Luminark\luminark.log`; releases with log rotation may also retain `luminark.log.old`. Windows may redirect application data for packaged installations. These files are not encrypted by Luminark and are subject to your Windows account's file permissions.

You can change preferences in Luminark. To remove saved preferences and logs, exit Luminark from its tray menu and delete those files from its application-data folder. Deleting `settings.json` resets saved preferences and monitor names; running Luminark again recreates settings and logs as needed. Version 1.1.0 appends to its log without automatic rotation. Local files can remain after uninstalling, so remove them separately if needed.

Enabling Start with Windows also creates a Windows startup registration (a current-user Run registry entry for unpackaged installations, or a Windows startup task for packaged installations). Disable this option in Luminark or Windows Startup Apps to stop automatic launch.

## Transmission and sharing

Luminark does not send your settings, monitor identifiers or diagnostic logs to the developer, and does not use advertising, behavioral analytics or telemetry services. The developer does not sell or rent this local information. No Luminark account is required.

Downloading Luminark, installing a required Microsoft .NET runtime, visiting repository links, or using Microsoft Store or Windows Package Manager involves the relevant third-party services and their own privacy policies. Those services may receive normal connection information, such as an IP address, independently of Luminark's local settings.

If you choose to submit an issue or attach a log on GitHub, the information you submit is shared with GitHub and, for public issues, is publicly visible. Review logs before sharing and remove monitor names, identifiers, local paths or other details you do not want to publish.

## Changes and contact

Updates to this policy will be published at this location with a revised date. For questions about Luminark's privacy practices, contact the maintainer through https://github.com/silviuk/Luminark/issues. Do not include private information in a public issue.
