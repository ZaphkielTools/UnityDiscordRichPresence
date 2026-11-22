import io
import struct
import sys

import pefile
from PIL import Image

RT_ICON = 3
RT_GROUP_ICON = 14
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
DISCORD_MIN_SIZE = 512


def largest_icon(exe_path):
    pe = pefile.PE(exe_path, fast_load=True)
    pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_RESOURCE"]])
    frames, groups = {}, []
    for resource_type in pe.DIRECTORY_ENTRY_RESOURCE.entries:
        for entry in resource_type.directory.entries:
            for language in entry.directory.entries:
                data = pe.get_data(language.data.struct.OffsetToData, language.data.struct.Size)
                if resource_type.id == RT_ICON:
                    frames[entry.id] = data
                elif resource_type.id == RT_GROUP_ICON:
                    groups.append(data)
    best = None
    for group in groups:
        count = struct.unpack_from("<H", group, 4)[0]
        for i in range(count):
            width, height, _, _, _, bpp, _, frame_id = struct.unpack_from("<BBBBHHIH", group, 6 + i * 14)
            data = frames.get(frame_id, b"")
            if not data.startswith(PNG_SIGNATURE):
                continue
            key = (width or 256, height or 256, bpp)
            if best is None or key > best[0]:
                best = (key, data)
    if best is None:
        raise SystemExit(f"No PNG icon frame in {exe_path}")
    return Image.open(io.BytesIO(best[1])).convert("RGBA")


def main():
    if len(sys.argv) != 3:
        raise SystemExit("usage: python extract_unity_icon.py <Unity.exe> <out.png>")
    icon = largest_icon(sys.argv[1])
    print(f"Largest frame: {icon.width}x{icon.height}")
    if icon.width < DISCORD_MIN_SIZE:
        icon = icon.resize((DISCORD_MIN_SIZE, DISCORD_MIN_SIZE), Image.LANCZOS)
    icon.save(sys.argv[2])
    print(f"Saved {sys.argv[2]} ({icon.width}x{icon.height})")


if __name__ == "__main__":
    main()
