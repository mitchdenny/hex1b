#!/usr/bin/env bash

set -euo pipefail

artifacts_dir="${1:?Usage: assert-nuget-contents.sh <artifacts-directory> <package-version>}"
package_version="${2:?Usage: assert-nuget-contents.sh <artifacts-directory> <package-version>}"
temp_dir=$(mktemp -d)
trap 'rm -rf "$temp_dir"' EXIT

extract_package() {
  local package_id="$1"
  local package_path="$artifacts_dir/$package_id.$package_version.nupkg"
  local destination="$temp_dir/$package_id"

  if [ ! -f "$package_path" ]; then
    echo "Package not found: $package_path"
    exit 1
  fi

  mkdir -p "$destination"
  unzip -q "$package_path" -d "$destination"
  echo "$destination"
}

assert_files_exist() {
  local package_name="$1"
  local package_root="$2"
  shift 2

  local missing=()
  local relative_path
  for relative_path in "$@"; do
    if [ ! -f "$package_root/$relative_path" ]; then
      missing+=("$relative_path")
    fi
  done

  if [ ${#missing[@]} -gt 0 ]; then
    echo "$package_name is missing required files:"
    printf '  %s\n' "${missing[@]}"
    exit 1
  fi
}

assert_directory_files() {
  local package_name="$1"
  local package_root="$2"
  local relative_directory="$3"
  shift 3

  local directory="$package_root/$relative_directory"
  local unexpected=()
  local actual_path
  local expected_path
  local is_expected

  while IFS= read -r actual_path; do
    is_expected=false
    for expected_path in "$@"; do
      if [ "$actual_path" = "$expected_path" ]; then
        is_expected=true
        break
      fi
    done

    if [ "$is_expected" = false ]; then
      unexpected+=("$relative_directory/$actual_path")
    fi
  done < <(find "$directory" -type f -printf '%P\n')

  if [ ${#unexpected[@]} -gt 0 ]; then
    echo "$package_name contains unexpected files:"
    printf '  %s\n' "${unexpected[@]}"
    exit 1
  fi
}

hex1b_root=$(extract_package "Hex1b")
mcp_root=$(extract_package "Hex1b.McpServer")
tool_root=$(extract_package "Hex1b.Tool")

hex1b_native_assets=(
  "runtimes/linux-x64/native/libhex1binterop.so"
  "runtimes/linux-arm64/native/libhex1binterop.so"
  "runtimes/osx-x64/native/libhex1binterop.dylib"
  "runtimes/osx-arm64/native/libhex1binterop.dylib"
  "runtimes/win-x64/native/hex1bpty.exe"
  "runtimes/win-x64/native/conpty.dll"
  "runtimes/win-x64/native/x64/OpenConsole.exe"
  "runtimes/win-x64/native/arm64/OpenConsole.exe"
  "runtimes/win-arm64/native/hex1bpty.exe"
  "runtimes/win-arm64/native/conpty.dll"
  "runtimes/win-arm64/native/arm64/OpenConsole.exe"
)

assert_files_exist "Hex1b" "$hex1b_root" \
  "lib/net8.0/Hex1b.dll" \
  "THIRD-PARTY-NOTICES.txt" \
  "${hex1b_native_assets[@]}"

assert_directory_files "Hex1b" "$hex1b_root" "runtimes/win-x64/native" \
  "hex1bpty.exe" "conpty.dll" "x64/OpenConsole.exe" "arm64/OpenConsole.exe"
assert_directory_files "Hex1b" "$hex1b_root" "runtimes/win-arm64/native" \
  "hex1bpty.exe" "conpty.dll" "arm64/OpenConsole.exe"

analyzer_leak=$(find "$hex1b_root" -type f -o -type d \
  | sed "s#^$hex1b_root/##" \
  | grep -iE '(^|/)Hex1b\.Analyzers(\.|/)|^analyzers(/|$)' || true)
if [ -n "$analyzer_leak" ]; then
  echo "Hex1b.Analyzers leaked into the Hex1b package:"
  echo "$analyzer_leak"
  exit 1
fi

assert_files_exist "Hex1b.McpServer" "$mcp_root" \
  "tools/net10.0/any/Hex1b.McpServer.dll" \
  "tools/net10.0/any/DotnetToolSettings.xml"

tool_native_assets=()
for asset in "${hex1b_native_assets[@]}"; do
  tool_native_assets+=("tools/net10.0/any/$asset")
done

tool_windows_graphics_assets=(
  "tools/net10.0/any/runtimes/win-x64/native/libHarfBuzzSharp.dll"
  "tools/net10.0/any/runtimes/win-x64/native/libSkiaSharp.dll"
  "tools/net10.0/any/runtimes/win-arm64/native/libHarfBuzzSharp.dll"
  "tools/net10.0/any/runtimes/win-arm64/native/libSkiaSharp.dll"
)

assert_files_exist "Hex1b.Tool" "$tool_root" \
  "tools/net10.0/any/Hex1b.Tool.dll" \
  "tools/net10.0/any/DotnetToolSettings.xml" \
  "${tool_native_assets[@]}" \
  "${tool_windows_graphics_assets[@]}"

assert_directory_files "Hex1b.Tool" "$tool_root" "tools/net10.0/any/runtimes/win-x64/native" \
  "hex1bpty.exe" "conpty.dll" "x64/OpenConsole.exe" "arm64/OpenConsole.exe" \
  "libHarfBuzzSharp.dll" "libSkiaSharp.dll"
assert_directory_files "Hex1b.Tool" "$tool_root" "tools/net10.0/any/runtimes/win-arm64/native" \
  "hex1bpty.exe" "conpty.dll" "arm64/OpenConsole.exe" \
  "libHarfBuzzSharp.dll" "libSkiaSharp.dll"

unexpected_tool_root_native=$(find "$tool_root/tools" -type f -printf '%P\n' \
  | grep -E '^[^/]+/any/libhex1binterop\.(so|dylib)$' || true)
if [ -n "$unexpected_tool_root_native" ]; then
  echo "Hex1b.Tool contains host-specific native libraries at the tool root:"
  echo "$unexpected_tool_root_native"
  exit 1
fi

echo "Verified extracted NuGet package contents:"
printf '  Hex1b/%s\n' "lib/net8.0/Hex1b.dll" "${hex1b_native_assets[@]}"
printf '  Hex1b.McpServer/%s\n' "tools/net10.0/any/Hex1b.McpServer.dll" "tools/net10.0/any/DotnetToolSettings.xml"
printf '  Hex1b.Tool/%s\n' "tools/net10.0/any/Hex1b.Tool.dll" "tools/net10.0/any/DotnetToolSettings.xml" "${tool_native_assets[@]}" "${tool_windows_graphics_assets[@]}"
