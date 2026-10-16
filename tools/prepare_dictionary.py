"""Download and reproducibly filter the official MIT-licensed ECDICT corpus.

Output words.tsv is UTF-8 without BOM, with no header and eight columns:
id, word, phonetic, translation, definition, tags, rank, exchange.
Literal backslash-n represents a definition line break.
"""
from __future__ import annotations

import collections
import csv
import datetime
import hashlib
import json
import pathlib
import re
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
DATA = ROOT / "data"
CACHE = ROOT / "tools" / "dictionary-source"
REPOSITORY = "https://github.com/skywind3000/ECDICT"
TAGS = ("zk", "gk", "cet4", "cet6", "ky", "ielts", "toefl", "gre")
WORD = re.compile(r"[A-Za-z]+(?:[ '-][A-Za-z]+)*\Z")
CHINESE = re.compile(r"[\u3400-\u9fff]")


def download(url: str, target: pathlib.Path) -> None:
    request = urllib.request.Request(url, headers={"User-Agent": "WorldSpeaceDictionaryBuilder/1.0"})
    with urllib.request.urlopen(request, timeout=120) as response, target.open("wb") as output:
        while chunk := response.read(1024 * 1024):
            output.write(chunk)


def sha256(path: pathlib.Path) -> str:
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def clean(value: str | None) -> str:
    # ECDICT includes literal \\n as well as normal line endings in some fields.
    value = (value or "").replace("\r\n", "\n").replace("\r", "\n")
    return value.replace("\t", " ").replace("\n", r"\n").strip()


def positive(value: str | None) -> int:
    try:
        result = int(value or "0")
        return result if result > 0 else 999999
    except ValueError:
        return 999999


def main() -> None:
    DATA.mkdir(parents=True, exist_ok=True)
    CACHE.mkdir(parents=True, exist_ok=True)
    api = "https://api.github.com/repos/skywind3000/ECDICT/commits/master"
    request = urllib.request.Request(api, headers={"User-Agent": "WorldSpeaceDictionaryBuilder/1.0"})
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            commit = json.load(response)["sha"]
    except urllib.error.HTTPError:
        # GitHub's unauthenticated API may be rate-limited; SHA256 below still
        # identifies the downloaded data exactly.
        commit = "master"
    raw = f"https://raw.githubusercontent.com/skywind3000/ECDICT/{commit}"
    original = CACHE / "ecdict.csv"
    download(f"{raw}/ecdict.csv", original)
    license_file = DATA / "ECDICT-LICENSE.txt"
    download(f"{raw}/LICENSE", license_file)

    counters = collections.Counter()
    selected: dict[str, tuple[list[str], int]] = {}
    with original.open(encoding="utf-8-sig", newline="") as source:
        for entry in csv.DictReader(source):
            counters["source_rows"] += 1
            word = (entry.get("word") or "").strip()
            if not WORD.fullmatch(word):
                counters["excluded_nonstandard_word"] += 1
                continue
            if not CHINESE.search(entry.get("translation") or ""):
                counters["excluded_no_chinese_translation"] += 1
                continue
            tags = set((entry.get("tag") or "").split())
            frq, bnc = positive(entry.get("frq")), positive(entry.get("bnc"))
            rank = frq if frq != 999999 else bnc
            if not tags.intersection(TAGS) and min(frq, bnc) > 10000:
                counters["excluded_outside_learning_scope"] += 1
                continue
            fields = [word.lower(), word, clean(entry.get("phonetic")),
                      clean(entry.get("translation")), clean(entry.get("definition")),
                      " ".join(tag for tag in TAGS if tag in tags), str(rank),
                      clean(entry.get("exchange"))]
            old = selected.get(fields[0])
            if old:
                counters["duplicate_ids"] += 1
                # Prefer the most useful exam/frequency entry for case variants.
                old_quality = (len(old[0][5].split()), -old[1], len(old[0][3]))
                quality = (len(fields[5].split()), -rank, len(fields[3]))
                if quality <= old_quality:
                    continue
            selected[fields[0]] = (fields, rank)

    rows = sorted(selected.values(), key=lambda item: (item[1], item[0][0]))
    counters["eligible_unique_rows"] = len(rows)
    counters["excluded_by_50000_cap"] = max(0, len(rows) - 50000)
    rows = rows[:50000]
    output = DATA / "words.tsv"
    with output.open("w", encoding="utf-8", newline="") as destination:
        for fields, _ in rows:
            assert len(fields) == 8 and all("\t" not in value and "\n" not in value for value in fields)
            destination.write("\t".join(fields) + "\n")
    tag_counts = collections.Counter(tag for fields, _ in rows for tag in fields[5].split())
    metadata = {
        "name": "ECDICT offline learning subset",
        "repository": REPOSITORY,
        "commit": commit if commit != "master" else None,
        "source_ref": commit,
        "commit_note": "GitHub API rate limit prevented resolving master; source_sha256 identifies the exact downloaded CSV." if commit == "master" else None,
        "downloaded_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "license": "MIT",
        "source_url": f"{raw}/ecdict.csv",
        "source_sha256": sha256(original),
        "license_sha256": sha256(license_file),
        "output": "words.tsv",
        "output_sha256": sha256(output),
        "encoding": "UTF-8 without BOM",
        "header": False,
        "columns": ["id", "word", "phonetic", "translation", "definition", "tags", "rank", "exchange"],
        "line_break_encoding": "literal backslash followed by n",
        "selection": {
            "word_pattern": WORD.pattern,
            "requires_chinese_translation": True,
            "include_exam_tags": list(TAGS),
            "or_top_frequency": 10000,
            "frequency_scope": "Either positive frq or bnc rank <= 10000",
            "rank": "positive frq, else positive bnc, else 999999",
            "deduplication": "lowercase word id; prefer more exam tags, then lower rank, then longer translation",
            "maximum_rows": 50000,
        },
        "counts": dict(counters),
        "output_rows": len(rows),
        "tag_counts": {tag: tag_counts[tag] for tag in TAGS},
    }
    (DATA / "source.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(metadata, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
