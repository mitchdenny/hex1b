#!/bin/bash
# Test KGP directly without Hex1b
# Run this in your Kitty terminal to verify KGP works

echo "=== Test 1: Tiny 2x2 red square (single chunk, no cursor control) ==="
printf '\033_Ga=T,f=32,s=2,v=2,c=2,r=1;/wAA//8AAP//AAD//wAA/w==\033\\'
echo ""
echo ""

echo "=== Test 2: Same but with i= and C=1 (our format) ==="
printf '\033_Ga=T,f=32,s=2,v=2,i=99,c=2,r=1,C=1,q=2;/wAA//8AAP//AAD//wAA/w==\033\\'
echo ""
echo ""

echo "=== Test 3: 4x4 red (our exact test data) ==="
printf '\033_Ga=T,f=32,s=4,v=4,i=98,c=4,r=1,C=1,q=2;/wAA//8AAP//AAD//wAA//8AAP//AAD//wAA//8AAP//AAD//wAA//8AAP//AAD//wAA//8AAP//AAD//wAA/w==\033\\'
echo ""
echo ""

echo "If you see colored squares above, KGP works."
echo "If not, check your terminal supports KGP."
