import struct
import zlib

with open(r'Assets/ghost-leviathan/source/Ghost_Leviathan.fbx', 'rb') as f:
    buf = f.read()

pos = buf.find(b'Vertices')
header = buf[pos+9:pos+9+12]
arr_len, encoding, comp_len = struct.unpack('<III', header)
print(f"Array length: {arr_len}, encoding: {encoding}, comp_len: {comp_len}")

comp_data = buf[pos+21 : pos+21+comp_len]
raw_data = zlib.decompress(comp_data)
print(f"Decompressed bytes: {len(raw_data)} (expected {arr_len * 8})")

coords = struct.unpack(f'<{arr_len}d', raw_data)
xs = coords[0::3]
ys = coords[1::3]
zs = coords[2::3]

print(f"X range: {min(xs):.3f} to {max(xs):.3f} (size = {max(xs)-min(xs):.3f})")
print(f"Y range: {min(ys):.3f} to {max(ys):.3f} (size = {max(ys)-min(ys):.3f})")
print(f"Z range: {min(zs):.3f} to {max(zs):.3f} (size = {max(zs)-min(zs):.3f})")
