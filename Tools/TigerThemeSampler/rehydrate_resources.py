#!/usr/bin/env python3
"""Rebuild hierarchy-preserving copies of extracted Tiger resources and PXM PNGs."""

from __future__ import annotations

import argparse
import csv
import re
import shutil
from pathlib import Path


def safe(value: str) -> str:
	value = value or "unnamed"
	return re.sub(r"[^A-Za-z0-9_.-]+", "_", value).strip("._") or "unnamed"


def read_tsv(path: Path) -> list[dict[str, str]]:
	with path.open("r", encoding="utf-8", newline="") as stream:
		return list(csv.DictReader(stream, delimiter="\t"))


def resource_relative_path(row: dict[str, str]) -> Path:
	source = Path(row["source"])
	try:
		system_index = source.parts.index("System")
		source_path = Path(*source.parts[system_index:])
	except ValueError:
		source_path = source
	resource_root = Path("sources") / source_path.with_suffix("")
	resource_type = safe(row["type"])
	resource_id = safe(row["id"])
	resource_name = safe(row["name"])
	payload_name = Path(row["payload"]).name
	return resource_root / resource_type / f"{resource_id}_{resource_name}" / payload_name


def png_relative_path(row: dict[str, str], payload_path: Path) -> Path:
	source_fork = Path(row["source"]).stem
	frame = int(row["frame"])
	resource_type = safe(row.get("type", "pxm#"))
	resource_id = safe(row.get("id", "unnamed"))
	resource_label = safe(row.get("name", payload_path.stem))
	resource_directory = f"{resource_id}_{resource_label}"
	return Path(source_fork) / resource_type / resource_directory / f"{payload_path.stem}.frame-{frame:02d}.png"


def rehydrate_resources(rows: list[dict[str, str]], destination: Path) -> tuple[int, int]:
	copied = failed = 0
	for row in rows:
		source = Path(*Path(row["payload"]).parts)
		output = destination / resource_relative_path(row)
		try:
			output.parent.mkdir(parents=True, exist_ok=True)
			shutil.copy2(source, output)
			copied += 1
		except OSError as error:
			print(f"ERROR resource {source}: {error}")
			failed += 1
	return copied, failed


def rehydrate_pngs(rows: list[dict[str, str]], resource_by_payload: dict[str, dict[str, str]], png_root: Path, destination: Path) -> tuple[int, int]:
	copied = failed = 0
	for row in rows:
		if row.get("status") != "ok":
			continue
		payload_name = Path(row["source"]).name
		resource = resource_by_payload.get(payload_name)
		if resource is None:
			print(f"ERROR PNG source is not in resource inventory: {row['source']}")
			failed += int(row["frames"]) if row.get("frames", "").isdigit() else 1
			continue
		source = Path(*Path(resource["payload"]).parts)
		try:
			frames = int(row["frames"])
		except (KeyError, ValueError):
			print(f"ERROR PNG metadata has no frame count: {row}")
			failed += 1
			continue

		for frame in range(frames):
			flat = png_root / f"{source.stem}.frame-{frame:02d}.png"
			output = destination / png_relative_path({**resource, "frame": str(frame)}, source)
			try:
				output.parent.mkdir(parents=True, exist_ok=True)
				shutil.copy2(flat, output)
				copied += 1
			except OSError as error:
				print(f"ERROR PNG {flat}: {error}")
				failed += 1
	return copied, failed


def main() -> int:
	parser = argparse.ArgumentParser(description=__doc__)
	parser.add_argument("resources", type=Path, help="Artifacts/Resources directory")
	parser.add_argument("destination", type=Path, help="Hierarchy-preserving output directory")
	args = parser.parse_args()

	extracted_root = args.resources / "extracted"
	png_root = args.resources / "pxm-png"
	resource_rows = read_tsv(extracted_root / "inventory.tsv")
	png_rows = read_tsv(png_root / "conversion.tsv")
	resource_by_payload = {Path(row["payload"]).name: row for row in resource_rows}

	resource_count, resource_failures = rehydrate_resources(resource_rows, args.destination / "payloads")
	png_destination = args.destination / "PNG"
	for source_fork in ("HIToolbox", "Extras"):
		(png_destination / source_fork).mkdir(parents=True, exist_ok=True)
	png_count, png_failures = rehydrate_pngs(png_rows, resource_by_payload, png_root, png_destination)

	print(f"Rehydrated {resource_count} resource payloads; {resource_failures} failed.")
	print(f"Rehydrated {png_count} PNG frames; {png_failures} failed.")
	return 1 if resource_failures or png_failures else 0


if __name__ == "__main__":
	raise SystemExit(main())
