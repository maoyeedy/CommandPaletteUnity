# rea: reverse-engineering toolkit

[morluto/rea](https://github.com/morluto/rea) (MIT, npm `rea-agents`) is an MCP server plus a `rea`
CLI that runs static analysis tools for agents. In this repo it is a research aid for checking
what Hub and the CLI store. It is never a build or runtime dependency of the extension.

Needs Node 22.23.4+, 24.22.1+ or 26.12.1+. Run with `npx -y rea-agents@latest <command> ... --json`.

## Tools that matter here

| Tool (CLI command) | Use in this repo | Notes |
|---|---|---|
| `inspect-sqlite-database` | Schema of `hub.db` and `install-state.db` (see [hub-data.md](hub-data.md)) | Opens a private copy with extensions disabled; never touches the source file. `--schema-scope database` for the schema only |
| `inspect-rust-binary` | Crate names and versions in `Unity Hub.exe` / `unity.exe` | Only reads `cargo-auditable` metadata. No result does not mean a crate is missing |
| `inspect-pe-imports`, `inspect-native-exports` | Which system DLLs Hub and the CLI link | Bundled SQLite does not show up |
| `capabilities` | Lists which providers are installed | Run first |

Not useful here: the JS/Electron analyzers (Hub 4 is native Rust; they only fit Hub 3), and the
Unity Mono/IL2CPP detection (that is for games, not the Hub).

## Rejected: deep native analysis

`list_strings` / `search_strings` (MCP via Ghidra) and the pseudocode/decompile tools (`analyze`,
`function`, `trace`) are **rejected, not deferred**. This is not a "look back if we need it" option:

- **No source to recover.** `unity.exe` is an optimized native Rust binary. Ghidra can return
  strings and pseudocode, never readable Rust source.
- **The facts we would dig for are already covered.** Crates and DLL links come from the standalone
  tools above; the data the extension cares about lives in `hub.db`, inspected directly with
  `inspect-sqlite-database`.
- **The CLI is observable without RE.** It has a stable, documented output contract
  (`--format json`, `unity commands --format tsv`; see [cli.md](cli.md)). Checking behavior from
  its output is cheaper and more reliable than reading disassembly.
- **Cost without payoff.** Ghidra 12.1+ and JDK 21+ are extra setup and Windows PE support is
  marked experimental. The remaining open questions in `cli.md` (for example, which store
  `projects pin` writes) are better answered by running the CLI on a dummy project and diffing
  `%APPDATA%\UnityHub`.
- **Runtime tools stay off-limits** (see [Safety](#safety)).

## Recipe: refresh the database schema

1. Copy `hub.db`, `hub.db-wal` and `install-state\install-state.db` from `%APPDATA%\UnityHub` into a
   new temp dir. Never copy `accounts.db` (personal data).
2. `npx -y rea-agents@latest inspect-sqlite-database <copy> --schema-scope database --json > <copy>.json`
3. Compare `schema.tables[].sql` with [hub-data.md](hub-data.md) and update it.

Last run 2026-11-18 against Hub 4.0.x: matches `hub-data.md`. The only addition was the
`writer_kind` column.

## Safety

- The static tools above never run the target binary.
- The runtime tools (process capture, Frida, GDB, Rizin debugging) do run it. Do not use them on Hub
  or the CLI.
- Analysis is local. Installing or updating rea downloads it from npm.
- Read the schema only. Leave row content out of the docs: `projects.data` holds paths and
  organization names.
- `rea setup` edits agent configs (with backups). It is not needed for CLI use.
