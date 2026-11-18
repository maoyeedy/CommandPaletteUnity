"""Structural snapshot of Unity Hub and Unity CLI data, safe to commit.

Records schemas, field names, counts, file names and command paths only:
no project paths, titles, ids, org names or account data.

Usage: python -I snapshot.py <out.json> [--diff <baseline.json>]
"""

import collections
import json
import os
import re
import shutil
import sqlite3
import subprocess
import sys
import tempfile

HUB = os.path.join(os.environ["APPDATA"], "UnityHub")
ENV = dict(os.environ, UNITY_NO_BANNER="1", UNITY_NON_INTERACTIVE="1", UNITY_NO_PAGER="1")


def cli(*args):
    try:
        r = subprocess.run(["unity", *args], capture_output=True, text=True, env=ENV, timeout=180, encoding="utf-8")
        return r.stdout
    except Exception as e:  # CLI missing or hung
        return f"<error {e}>"


def cli_json(*args):
    try:
        return json.loads(cli(*args, "--json"))
    except Exception:
        return None


def read_json(name):
    try:
        with open(os.path.join(HUB, name), encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return None


def db_copy(rel):
    """Copy a WAL database with its sidecars so Hub's locks do not matter."""
    src = os.path.join(HUB, rel)
    if not os.path.exists(src):
        return None
    tmp = tempfile.mkdtemp()
    for suffix in ("", "-wal", "-shm"):
        if os.path.exists(src + suffix):
            shutil.copy2(src + suffix, os.path.join(tmp, os.path.basename(src) + suffix))
    return sqlite3.connect(os.path.join(tmp, os.path.basename(src)))


def schema(con):
    if con is None:
        return None
    out = {}
    for name, sql in con.execute("select name, sql from sqlite_master where type='table' order by name"):
        out[name] = {"sql": re.sub(r"\s+", " ", sql), "rows": con.execute(f'select count(*) from "{name}"').fetchone()[0]}
    return out


def fingerprint_suffix(fp):
    # Keep only the tag part after '|', with numbers masked.
    return re.sub(r"\d+", "N", fp.split("|", 1)[1]) if fp and "|" in fp else None


def summarize_records(records):
    keys = collections.Counter()
    types = collections.defaultdict(set)
    suffixes = collections.Counter()
    for r in records:
        keys.update(r.keys())
        for k, v in r.items():
            types[k].add(type(v).__name__)
        suffixes[fingerprint_suffix(r.get("sourceFingerprint"))] += 1
    return {
        "count": len(records),
        "fields": {k: {"present": keys[k], "types": sorted(types[k])} for k in sorted(keys)},
        "fingerprintSuffixes": {str(k): v for k, v in suffixes.items()},
    }


def main():
    out_path = sys.argv[1]
    snap = {}

    info = read_json("hubInfo.json") or {}
    snap["hubVersion"] = info.get("version")
    snap["cliVersion"] = cli("--version").strip() or None

    snap["dataFolder"] = {
        d: sorted(os.listdir(os.path.join(HUB, d))) if os.path.isdir(os.path.join(HUB, d)) else None
        for d in ("", "hub-gpui", "install-state", "logs")
    }
    # Log names carry pids; mask them.
    if snap["dataFolder"]["logs"]:
        snap["dataFolder"]["logs"] = sorted({re.sub(r"\d+-[0-9a-f]{32}", "PID-ID", n) for n in snap["dataFolder"]["logs"]})

    hub = db_copy("hub.db")
    snap["hubDb"] = schema(hub)
    db_rows = []
    if hub is not None:
        snap["hubDbMeta"] = dict(hub.execute("select key, value from meta")) if "meta" in snap["hubDb"] else None
        db_rows = [(p, json.loads(d)) for p, d in hub.execute("select path, data from projects")]
    snap["hubDbProjects"] = summarize_records([d for _, d in db_rows])

    snap["otherDbs"] = {rel: schema(db_copy(rel)) for rel in ("install-state/install-state.db", "accounts.db")}
    # Row counts of accounts are personal-adjacent but harmless; drop them anyway.
    if snap["otherDbs"]["accounts.db"]:
        for t in snap["otherDbs"]["accounts.db"].values():
            t.pop("rows", None)

    v1 = (read_json("projects-v1.json") or {}).get("data") or {}
    snap["projectsV1Json"] = summarize_records(list(v1.values()))
    excluded = set((read_json("excludedProjectFolders.json") or {}).get("data") or [])

    # Shapes of small JSON settings files: top-level keys only.
    shapes = {}
    for n in snap["dataFolder"][""] or []:
        p = os.path.join(HUB, n)
        if n.endswith(".json") and os.path.getsize(p) < 20000:
            j = read_json(n)
            if isinstance(j, dict):
                # Path-keyed maps (projectsInfo.json, ...) would leak paths.
                shapes[n] = "<path-keyed map>" if any(re.match(r"^[A-Za-z]:|^/", k) for k in j) else sorted(j)
            else:
                shapes[n] = type(j).__name__
    snap["jsonShapes"] = shapes

    # Which store does the CLI read? Compare path sets and tag/fingerprint evidence.
    cp = cli_json("projects", "--all")
    cli_items = (cp or {}).get("data", {}).get("items") or []
    cli_paths = {i.get("path") for i in cli_items}
    db_paths = {p for p, _ in db_rows}
    v1_paths = set(v1) - excluded
    snap["cliProjects"] = summarize_records(cli_items)
    snap["cliProjects"]["envelopeKeys"] = sorted(cp) if cp else None
    snap["cliProjects"]["dataKeys"] = sorted(cp["data"]) if cp else None
    snap["cliProjects"]["matches"] = {
        "hubDb": cli_paths == db_paths,
        "projectsV1MinusExcluded": cli_paths == v1_paths,
    }
    snap["hubVsJson"] = {
        "onlyInDb": len(db_paths - set(v1)),
        "onlyInJson": len(set(v1) - db_paths),
        "rowsWithTags": sum(1 for _, d in db_rows if d.get("tags")),
        "cliRowsWithTags": sum(1 for i in cli_items if i.get("tags")),
    }

    # Editors: folder scan vs CLI.
    root = (read_json("secondaryInstallPath.json") or "") or os.path.join(os.environ.get("ProgramFiles", r"C:\Program Files"), "Unity", "Hub", "Editor")
    folder = sorted(v for v in (os.listdir(root) if os.path.isdir(root) else []) if os.path.exists(os.path.join(root, v, "Editor", "Unity.exe")))
    ei = cli_json("editors", "-i")
    ea = cli_json("editors")
    cli_inst = sorted(i["version"] for i in (ei or {}).get("data", {}).get("items", []))
    snap["editors"] = {
        "installFolderIsDefault": not read_json("secondaryInstallPath.json"),
        "folderScanCount": len(folder),
        "cliInstalledCount": len(cli_inst),
        "cliMissesFolderEditors": len(set(folder) - set(cli_inst)),
        "cliInstalledItemKeys": sorted({k for i in (ei or {}).get("data", {}).get("items", []) for k in i}),
        "cliDefaultListMixesReleases": any("location" not in i for i in (ea or {}).get("data", {}).get("items", [])),
        "editorsV2Count": len((read_json("editors-v2.json") or {}).get("data") or []),
    }

    cmds = cli_json("commands") or {}
    items = cmds.get("data", {}).get("items") if isinstance(cmds.get("data"), dict) else cmds.get("data")
    if not items:
        raw = cli("commands", "--format", "tsv", "--no-header")
        items = [{"path": line.split("\t")[0]} for line in raw.splitlines() if "\t" in line]
    snap["cliCommands"] = sorted({i.get("path") for i in items or [] if i.get("path")})

    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(snap, f, indent=1, sort_keys=True)
        f.write("\n")

    if "--diff" in sys.argv:
        base = json.load(open(sys.argv[sys.argv.index("--diff") + 1], encoding="utf-8"))
        diff(base, snap, "")


def diff(a, b, path):
    if isinstance(a, dict) and isinstance(b, dict):
        for k in sorted(set(a) | set(b)):
            if k not in a:
                print(f"+ {path}/{k}: {json.dumps(b[k])[:200]}")
            elif k not in b:
                print(f"- {path}/{k}")
            else:
                diff(a[k], b[k], f"{path}/{k}")
    elif isinstance(a, list) and isinstance(b, list) and all(isinstance(x, str) for x in a + b):
        for x in sorted(set(b) - set(a)):
            print(f"+ {path}[] {x}")
        for x in sorted(set(a) - set(b)):
            print(f"- {path}[] {x}")
    elif a != b:
        print(f"~ {path}: {json.dumps(a)[:120]} -> {json.dumps(b)[:120]}")


if __name__ == "__main__":
    main()
