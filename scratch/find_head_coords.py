import struct
import zlib

with open(r'Assets/ghost-leviathan/source/Ghost_Leviathan.fbx', 'rb') as f:
    buf = f.read()

# Decompress all vertices
pos = buf.find(b'Vertices')
header = buf[pos+9:pos+9+12]
arr_len, encoding, comp_len = struct.unpack('<III', header)
comp_data = buf[pos+21 : pos+21+comp_len]
raw_data = zlib.decompress(comp_data)
coords = struct.unpack(f'<{arr_len}d', raw_data)

# Read head deformer indices
pos = buf.find(b'head\x00\x01SubDeformer')
print("Found head deformer at", pos)
# Find Indexes array
idx_pos = buf.find(b'Indexes', pos)
print("Indexes at", idx_pos)
h = buf[idx_pos+8 : idx_pos+8+12]
# let's inspect bytes
print(list(buf[idx_pos:idx_pos+25]))

# if compressed or not
t_code = buf[idx_pos+7]
num_indices, enc, c_len = struct.unpack('<III', buf[idx_pos+8:idx_pos+20])
if enc == 1:
    idx_raw = zlib.decompress(buf[idx_pos+20 : idx_pos+20+c_len])
    indices = struct.unpack(f'<{num_indices}i', idx_raw)
else:
    indices = struct.unpack(f'<{num_indices}i', buf[idx_pos+20 : idx_pos+20+num_indices*4])

head_zs = [coords[i*3 + 2] for i in indices]
print(f"Head vertices Z range: {min(head_zs):.3f} to {max(head_zs):.3f}")
