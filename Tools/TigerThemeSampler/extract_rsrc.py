#!/usr/bin/env python3
"""Extract classic Mac Resource Manager resources from .rsrc files."""

import argparse
import csv
import re
import struct
from pathlib import Path


def u16(data, offset):
	return struct.unpack_from(">H", data, offset)[0]


def s16(data, offset):
	return struct.unpack_from(">h", data, offset)[0]


def u32(data, offset):
	return struct.unpack_from(">I", data, offset)[0]


def decode_name(raw):
	if not raw:
		return ""
	try:
		return raw.decode("mac_roman", "replace")
	except LookupError:
		return raw.decode("latin1", "replace")


def safe(value):
	value = value or "unnamed"
	return re.sub(r"[^A-Za-z0-9_.-]+", "_", value).strip("._") or "unnamed"


def parse_resources(path):
	data = path.read_bytes()
	if len(data) < 16:
		raise ValueError("resource fork is smaller than its header")

	data_offset = u32(data, 0)
	map_offset = u32(data, 4)
	data_length = u32(data, 8)
	map_length = u32(data, 12)

	if data_offset + data_length > len(data):
		raise ValueError("resource data area exceeds file")
	if map_offset + map_length > len(data):
		raise ValueError("resource map exceeds file")

	map_base = map_offset
	type_offset = u16(data, map_base + 24)
	name_offset = u16(data, map_base + 26)
	type_list = map_base + type_offset
	name_list = map_base + name_offset
	type_count = u16(data, type_list) + 1
	result = []

	for type_index in range(type_count):
		type_entry = type_list + 2 + type_index * 8
		resource_type = data[type_entry:type_entry + 4].decode("mac_roman", "replace")
		resource_count = u16(data, type_entry + 4) + 1
		ref_offset = u16(data, type_entry + 6)
		refs = type_list + ref_offset

		for resource_index in range(resource_count):
			ref = refs + resource_index * 12
			resource_id = s16(data, ref)
			name_offset_value = s16(data, ref + 2)
			attributes = data[ref + 4]
			data_offset_24 = int.from_bytes(data[ref + 5:ref + 8], "big")
			payload_header = data_offset + data_offset_24
			payload_length = u32(data, payload_header)
			payload_start = payload_header + 4
			payload_end = payload_start + payload_length
			if payload_end > len(data):
				raise ValueError(f"{resource_type} {resource_id} payload exceeds file")

			name = ""
			if name_offset_value >= 0:
				name_start = name_list + name_offset_value
				name_length = data[name_start]
				name = decode_name(data[name_start + 1:name_start + 1 + name_length])

			result.append({
				"type": resource_type,
				"id": resource_id,
				"name": name,
				"attributes": attributes,
				"offset": data_offset_24,
				"length": payload_length,
				"payload": data[payload_start:payload_end],
			})

	return result


def main():
	parser = argparse.ArgumentParser()
	parser.add_argument("source", type=Path)
	parser.add_argument("output", type=Path)
	args = parser.parse_args()
	args.output.mkdir(parents=True, exist_ok=True)
	inventory_path = args.output / "inventory.tsv"
	resources = []

	source_files = sorted(args.source.rglob("*.rsrc")) if args.source.is_dir() else [args.source]
	for source in source_files:
		try:
			parsed = parse_resources(source)
		except Exception as error:
			print(f"ERROR {source}: {error}")
			continue
		source_tag = safe(source.stem)
		source_output = args.output / source_tag
		source_output.mkdir(parents=True, exist_ok=True)
		for index, resource in enumerate(parsed):
			base = f"{resource['type']}_{resource['id']}_{safe(resource['name'])}_{index}"
			payload_path = source_output / f"{base}.bin"
			payload_path.write_bytes(resource["payload"])
			resources.append({
				"source": str(source),
				"type": resource["type"],
				"id": resource["id"],
				"name": resource["name"],
				"attributes": resource["attributes"],
				"length": resource["length"],
				"payload": str(payload_path),
			})
		print(f"{source}: {len(parsed)} resources")

	with inventory_path.open("w", newline="", encoding="utf-8") as stream:
		fields = ["source", "type", "id", "name", "attributes", "length", "payload"]
		writer = csv.DictWriter(stream, fieldnames=fields, delimiter="\t")
		writer.writeheader()
		writer.writerows(resources)
	print(f"Wrote {len(resources)} resources to {inventory_path}")


if __name__ == "__main__":
	main()
