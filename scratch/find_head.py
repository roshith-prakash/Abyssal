import struct

with open(r'Assets/ghost-leviathan/source/Ghost_Leviathan.fbx', 'rb') as f:
    buf = f.read()

# Let's find "head" and "Lcl Translation"
idx = 0
while True:
    pos = buf.find(b'head', idx)
    if pos == -1:
        break
    context = buf[max(0, pos-20):pos+80]
    print(f"Match at {pos}: {context}")
    idx = pos + 4
    if len(context) > 0 and b'Model::' in context:
        # print next 300 bytes
        print("Model block:", buf[pos:pos+300])
