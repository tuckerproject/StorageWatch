# StorageWatch

StorageWatch is a self-hosted disk monitoring solution with three components:

- **StorageWatchAgent**: Windows service that monitors local drives, stores metrics in SQLite, and sends alerts.
- **StorageWatchServer**: Central ASP.NET Core server that ingests agent reports and hosts a web dashboard.
- **StorageWatchUI**: WPF desktop app for local monitoring and service control.

## Documentation

All active project documentation is centralized in:

- **[/Docs/README.md](/Docs/README.md)**

## License

CC0 1.0 Universal (Public Domain). See [/LICENSE](/LICENSE).

## Third-party notices

StorageWatch source code is released under CC0. Distributed binaries include
third-party NuGet dependencies licensed under MIT, Apache-2.0, BSD-3-Clause,
and SQLite public-domain terms. Redistribution must retain the applicable
copyright notices and license texts. The resolved dependency inventory is in
[/THIRD-PARTY-NOTICES.txt](/THIRD-PARTY-NOTICES.txt), and the corresponding
license texts are in [/licenses](/licenses).
