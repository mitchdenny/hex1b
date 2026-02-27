#!/bin/bash
# Minimal KGP test - run inside Kitty/WezTerm/Ghostty
# This tests whether the terminal supports KGP at all

# 4x4 red square RGBA (4*4*4=64 bytes)
# Each pixel: R=255, G=0, B=0, A=255
DATA=$(python3 -c "
import base64
pixels = b''
for i in range(16):  # 4x4 pixels
    pixels += bytes([255, 0, 0, 255])  # red, RGBA
print(base64.b64encode(pixels).decode())
")

echo "Testing KGP with 4x4 red square..."
echo "Data length: ${#DATA} bytes"
echo "Data: $DATA"
echo ""

# Method 1: Simple single-chunk a=T
printf '\x1b_Ga=T,f=32,s=4,v=4,q=2;%s\x1b\\' "$DATA"
echo ""
echo "^ If you see a red square above, KGP works"
echo ""

# Method 2: With image ID and display size
printf '\x1b_Ga=T,f=32,s=4,v=4,i=99,c=8,r=4,C=1,q=2;%s\x1b\\' "$DATA"
echo ""
echo "^ Same image scaled to 8 cols x 4 rows"
