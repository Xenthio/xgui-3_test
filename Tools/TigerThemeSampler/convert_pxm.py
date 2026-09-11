#!/usr/bin/env python3
"""Export extracted Tiger pxm# resources as PNG images."""

from __future__ import annotations

import argparse
import binascii
import struct
from pathlib import Path


HEADER_SIZE = 24
SHARED_MASK = 0x0001


def png_chunk(kind: bytes, payload: bytes) -> bytes:
	return (
		struct.pack(">I", len(payload))
		+ kind
		+ payload
		+ struct.pack(">I", binascii.crc32(kind + payload) & 0xFFFFFFFF)
	)


def write_png(path: Path, width: int, height: int, rgba: bytes) -> None:
	scanlines = b"".join(
		b"\x00" + rgba[row * width * 4 : (row + 1) * width * 4]
		for row in range(height)
	)
	data = (
		b"\x89PNG\r\n\x1a\n"
		+ png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
		+ png_chunk(b"IDAT", __import__("zlib").compress(scanlines, 9))
		+ png_chunk(b"IEND", b"")
	)
	path.write_bytes(data)


def decode_pxm(source: Path, destination: Path) -> tuple[int, int, int, int]:
	payload = source.read_bytes()
	if len(payload) < HEADER_SIZE:
		raise ValueError("resource is shorter than its header")

	version, flags = struct.unpack_from(">HH", payload, 0)
	height, width = struct.unpack_from(">HH", payload, 8)
	frame_count = struct.unpack_from(">H", payload, 22)[0]
	if version != 3:
		raise ValueError(f"unsupported pxm version: {version}")
	image_size = width * height * 4
	mask_row_bytes = (width + 15) // 16 * 2
	mask_size = mask_row_bytes * height
	if not flags & SHARED_MASK:
		mask_size *= frame_count
	image_start = HEADER_SIZE + mask_size
	image_area = image_size * frame_count
	remaining = len(payload) - image_start

	if not width or not height or not frame_count:
		raise ValueError(f"invalid dimensions/count: {width}x{height}, {frame_count} frames")
	if remaining < image_area:
		raise ValueError(
			f"payload is too short for {frame_count} {width}x{height} RGBA frames after mask"
		)
	destination.mkdir(parents=True, exist_ok=True)
	for frame in range(frame_count):
		start = image_start + frame * image_size
		image = payload[start : start + image_size]
		output = destination / f"{source.stem}.frame-{frame:02d}.png"
		write_png(output, width, height, image)

	return width, height, frame_count, len(payload) - image_start - image_area


def main() -> int:
	parser = argparse.ArgumentParser(description=__doc__)
	parser.add_argument("source", type=Path, help="extracted resource directory or pxm# file")
	parser.add_argument("destination", type=Path, help="PNG output directory")
	args = parser.parse_args()

	sources = [args.source] if args.source.is_file() else sorted(args.source.rglob("pxm#_*.bin"))
	args.destination.mkdir(parents=True, exist_ok=True)
	report = args.destination / "conversion.tsv"
	converted = failed = 0

	with report.open("w", encoding="utf-8", newline="") as stream:
		stream.write("source\twidth\theight\tframes\ttrailing_metadata_bytes\tstatus\n")
		for source in sources:
			try:
				width, height, frames, metadata = decode_pxm(source, args.destination)
				stream.write(f"{source}\t{width}\t{height}\t{frames}\t{metadata}\tok\n")
				converted += frames
			except (OSError, ValueError, struct.error) as error:
				stream.write(f"{source}\t\\t\\t\\t\\tfailed: {error}\n")
				failed += 1

	print(f"Converted {converted} PNG frames from {len(sources)} pxm# resources; {failed} failed.")
	print(f"Report: {report}")
	return 1 if failed else 0


if __name__ == "__main__":
	raise SystemExit(main())
