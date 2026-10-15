#!/usr/bin/env python3
"""Generate the website's version and changelog data from git tags.

Single source of truth: the repository's semver tags (``vX.Y.Z`` with an
optional codename suffix such as ``-roadmap-execution``). Each entry's notes
come from the matching ``## [<tag>]`` section of docs/community/CHANGELOG.md.
GitHub Releases are not used.

Writes:
  site/data/version.json    newest tag (normalised version + raw tag)
  site/data/changelog.json  every tag, newest first, with its notes

Usage:
  python scripts/site-data.py            # write the JSON files
  python scripts/site-data.py --check    # also fail if Directory.Build.props
                                          # <Version> differs from the newest tag
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CHANGELOG = ROOT / "docs" / "community" / "CHANGELOG.md"
PROPS = ROOT / "Directory.Build.props"
OUT_DIR = ROOT / "site" / "data"
REPO_URL = "https://github.com/FittyAr/Cardscape"
CHANGELOG_BLOB = f"{REPO_URL}/blob/master/docs/community/"
TAG_RE = re.compile(r"^v(\d+)\.(\d+)\.(\d+)(?:-(.+))?$")


def git(*args: str) -> str:
    return subprocess.run(["git", *args], cwd=ROOT, check=True,
                          capture_output=True, text=True, encoding="utf-8").stdout


def read_tags() -> list[dict]:
    fmt = "%(refname:short)%09%(creatordate:short)%09%(contents:subject)"
    tags = []
    for line in git("for-each-ref", "refs/tags", f"--format={fmt}").splitlines():
        name, date, subject = (line.split("\t") + ["", ""])[:3]
        m = TAG_RE.match(name)
        if not m:
            continue  # e.g. backup/... tags
        major, minor, patch, suffix = m.groups()
        tags.append({"tag": name, "version": f"v{major}.{minor}.{patch}",
                     "key": (int(major), int(minor), int(patch)),
                     "codename": suffix or "", "date": date, "subject": subject})
    tags.sort(key=lambda t: t["key"], reverse=True)
    return tags


def changelog_sections() -> dict[str, dict]:
    """Map '## [tag] — date' headings to their (date, body)."""
    sections: dict[str, dict] = {}
    current = None
    for line in CHANGELOG.read_text(encoding="utf-8").splitlines():
        head = re.match(r"^## \[([^\]]+)\](?:\s*[—-]\s*(\d{4}-\d{2}-\d{2}))?", line)
        if head:
            current = {"date": head.group(2) or "", "lines": []}
            sections[head.group(1)] = current
        elif current is not None:
            if line.strip() == "---":
                current = None
            else:
                current["lines"].append(line)
    return sections


def absolutise_links(text: str) -> str:
    """Relative links in the changelog point next to it; make them absolute."""
    def fix(m: re.Match) -> str:
        label, href = m.group(1), m.group(2)
        if re.match(r"^(https?:|mailto:|#)", href):
            return m.group(0)
        return f"[{label}]({CHANGELOG_BLOB}{href})"
    return re.sub(r"\[([^\]]+)\]\(([^)\s]+)\)", fix, text)


def normalise_body(lines: list[str]) -> str:
    """Collapse hard-wrapped Markdown into one line per heading/bullet/paragraph.

    The site renders a small subset (headings, bullets, paragraphs, inline
    code/bold/links); code fences and tables are dropped.
    """
    blocks: list[str] = []
    current: str | None = None
    in_fence = False

    def flush() -> None:
        nonlocal current
        if current is not None and current.strip():
            blocks.append(current.strip())
        current = None

    for raw in lines:
        line = raw.rstrip()
        stripped = line.strip()
        if stripped.startswith("```"):
            in_fence = not in_fence
            flush()
            continue
        if in_fence or stripped.startswith("|"):
            continue
        if not stripped:
            flush()
        elif stripped.startswith("#"):
            flush()
            blocks.append(stripped)
        elif re.match(r"^[-*]\s+", stripped) or re.match(r"^\d+\.\s+", stripped):
            flush()
            current = "- " + re.sub(r"^([-*]|\d+\.)\s+", "", stripped)
        elif current is None:
            current = stripped
        else:
            current += " " + stripped
    flush()
    return absolutise_links("\n".join(blocks))


def props_version() -> str:
    m = re.search(r"<Version>([^<]+)</Version>", PROPS.read_text(encoding="utf-8"))
    return m.group(1).strip() if m else ""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--check", action="store_true",
                        help="fail if Directory.Build.props <Version> != newest tag")
    args = parser.parse_args()

    tags = read_tags()
    if not tags:
        print("No vX.Y.Z tags found (shallow clone without tags?).", file=sys.stderr)
        return 1
    sections = changelog_sections()

    entries = []
    for t in tags:
        section = sections.get(t["tag"]) or sections.get(t["version"])
        body = normalise_body(section["lines"]) if section else ""
        subject = re.sub(rf"^{re.escape(t['tag'])}:\s*", "", t["subject"])
        if not body and subject and subject != t["tag"]:
            body = subject
        entries.append({
            "tag": t["tag"], "version": t["version"], "codename": t["codename"],
            "date": (section or {}).get("date") or t["date"],
            "url": f"{REPO_URL}/releases/tag/{t['tag']}",
            "body": body,
        })

    latest = entries[0]
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUT_DIR / "version.json").write_text(json.dumps({
        "version": latest["version"], "tag": latest["tag"],
        "date": latest["date"], "url": latest["url"],
    }, indent=2) + "\n", encoding="utf-8")
    (OUT_DIR / "changelog.json").write_text(
        json.dumps(entries, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"site/data: latest {latest['version']} ({latest['tag']}), {len(entries)} entries")

    code_version = "v" + props_version().lstrip("v")
    if code_version != latest["version"]:
        msg = (f"Directory.Build.props <Version> is {code_version} but the newest tag is "
               f"{latest['tag']} ({latest['version']}). Bump <Version> when tagging.")
        if args.check:
            print(f"::error file=Directory.Build.props::{msg}")
            return 1
        print(f"::warning file=Directory.Build.props::{msg}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
