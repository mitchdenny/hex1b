#!/bin/bash
# Emoji Width Test Script
# Run this to check if your terminal correctly handles emoji width

echo ""
echo "=== Test 1: Relative positioning ==="
echo "The | should all be at the same column:"
printf "📁 Folder  |\n"
printf "📄 Document|\n"
printf "🖼️ Pictures|\n"
printf "📷 Camera  |\n"

echo ""
echo "=== Test 2: Box test ==="
echo "All right borders should align vertically:"
echo "┌──────────────┐"
echo "│ 📁 Folder    │"
echo "│ 📄 Document  │"
echo "│ 🖼️ Pictures  │"
echo "│ 📷 Camera    │"
echo "└──────────────┘"

echo ""
echo "=== Test 3: Padding comparison ==="
echo "All END markers should align:"
printf "A📁B         END\n"
printf "A📄B         END\n"
printf "A🖼️B         END\n"
printf "A📷B         END\n"

echo ""
echo "=== Results ==="
echo "If the 🖼️ line is misaligned in any test above,"
echo "your terminal has emoji width calculation issues."
echo ""
