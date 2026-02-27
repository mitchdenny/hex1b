#!/bin/bash
# Test KGP source rectangle clipping (x,y,w,h parameters)
# Run in a KGP-capable terminal (Kitty, WezTerm, etc.)

# Create a 4x4 RGBA red/green/blue/yellow quadrant image (32 bytes)
# Top-left=red, Top-right=green, Bottom-left=blue, Bottom-right=yellow
# Each quadrant is 2x2 pixels
PIXELS=""
# Row 0: red red green green
PIXELS+=$(printf '\xff\x00\x00\xff' | xxd -p)  # red
PIXELS+=$(printf '\xff\x00\x00\xff' | xxd -p)  # red
PIXELS+=$(printf '\x00\xff\x00\xff' | xxd -p)  # green
PIXELS+=$(printf '\x00\xff\x00\xff' | xxd -p)  # green
# Row 1: red red green green
PIXELS+=$(printf '\xff\x00\x00\xff' | xxd -p)  # red
PIXELS+=$(printf '\xff\x00\x00\xff' | xxd -p)  # red
PIXELS+=$(printf '\x00\xff\x00\xff' | xxd -p)  # green
PIXELS+=$(printf '\x00\xff\x00\xff' | xxd -p)  # green
# Row 2: blue blue yellow yellow
PIXELS+=$(printf '\x00\x00\xff\xff' | xxd -p)  # blue
PIXELS+=$(printf '\x00\x00\xff\xff' | xxd -p)  # blue
PIXELS+=$(printf '\xff\xff\x00\xff' | xxd -p)  # yellow
PIXELS+=$(printf '\xff\xff\x00\xff' | xxd -p)  # yellow
# Row 3: blue blue yellow yellow
PIXELS+=$(printf '\x00\x00\xff\xff' | xxd -p)  # blue
PIXELS+=$(printf '\x00\x00\xff\xff' | xxd -p)  # blue
PIXELS+=$(printf '\xff\xff\x00\xff' | xxd -p)  # yellow
PIXELS+=$(printf '\xff\xff\x00\xff' | xxd -p)  # yellow

B64=$(echo -n "$PIXELS" | xxd -r -p | base64 -w0)

echo "=== KGP Source Rectangle Clipping Test ==="
echo ""

# Test 1: Transmit image (a=t) then display full image (a=p)
echo "Test 1: Full image (should show 4 quadrants: red/green/blue/yellow)"
printf '\e_Ga=t,f=32,s=4,v=4,i=99,q=2;%s\e\\' "$B64"
printf '\e_Ga=p,i=99,c=8,r=4,q=2\e\\'
echo ""
echo ""

# Test 2: Display only left half (x=0, w=2)
echo "Test 2: Left half only (should show red/blue)"
printf '\e_Ga=p,i=99,x=0,y=0,w=2,h=4,c=4,r=4,q=2\e\\'
echo ""
echo ""

# Test 3: Display only top-right quadrant (x=2, y=0, w=2, h=2)
echo "Test 3: Top-right quadrant only (should show green)"
printf '\e_Ga=p,i=99,x=2,y=0,w=2,h=2,c=4,r=2,q=2\e\\'
echo ""
echo ""

# Test 4: Display only bottom half (y=2, h=2)
echo "Test 4: Bottom half only (should show blue/yellow)"
printf '\e_Ga=p,i=99,x=0,y=2,w=4,h=2,c=8,r=2,q=2\e\\'
echo ""
echo ""

# Test 5: Same as test 2 but with z=-1 (under text)
echo "Test 5: Left half with z=-1 (should show red/blue UNDER this text if z works)"
printf '\e_Ga=p,i=99,x=0,y=0,w=2,h=4,c=8,r=4,z=-1,q=2\e\\'
echo ""
echo ""

echo "=== Done ==="
echo "If tests 2-4 show cropped portions, source rect clipping works."
echo "If they show the full image or nothing, the terminal may not support it."
