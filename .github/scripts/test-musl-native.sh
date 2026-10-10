#!/usr/bin/env bash
set -euo pipefail

# Run inside an x64 Alpine .NET SDK image with build-base, bash, binutils and unzip.
test -f /etc/alpine-release
test "$(uname -m)" = x86_64
for package in gcompat libc6-compat; do
    if apk info -e "$package"; then
        echo "Runtime validation must not use glibc compatibility packages: $package" >&2
        exit 1
    fi
done

temp_dir="$(mktemp -d)"
trap 'rm -rf "$temp_dir"' EXIT
fixture=tests/Fixtures/MuslPtySmoke/MuslPtySmoke.csproj
library=src/Hex1b/runtimes/linux-musl-x64/native/libhex1binterop.so
version=0.0.0-musl-smoke

# Force an automatic native build, even when a prebuilt asset is present.
rm -f "$library" src/Hex1b/obj/native/linux-musl-x64/hex1binterop.o
dotnet build "$fixture" -c Release -p:UseHex1bPackageReference=false -o "$temp_dir/project"
cmp "$library" "$temp_dir/project/libhex1binterop.so"
dotnet "$temp_dir/project/MuslPtySmoke.dll"
make -C src/Hex1b/native all test-startup check-exports
bash .github/scripts/assert-musl-native.sh "$library"

dotnet pack src/Hex1b/Hex1b.csproj -c Release -p:PackageVersion="$version" -o "$temp_dir/packages"
unzip -p "$temp_dir/packages/Hex1b.$version.nupkg" \
    runtimes/linux-musl-x64/native/libhex1binterop.so > "$temp_dir/packaged.so"
cmp "$library" "$temp_dir/packaged.so"

cat > "$temp_dir/NuGet.config" <<EOF
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$temp_dir/packages" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local"><package pattern="Hex1b" /></packageSource>
    <packageSource key="nuget"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
EOF

# Use an isolated cache so a prior smoke package cannot mask packaging changes.
NUGET_PACKAGES="$temp_dir/cache" dotnet publish "$fixture" -c Release \
    -r linux-musl-x64 --self-contained false \
    -p:UseHex1bPackageReference=true -p:Hex1bPackageVersion="$version" \
    --configfile "$temp_dir/NuGet.config" -o "$temp_dir/publish"
grep -Fq '"runtimes/linux-musl-x64/native/libhex1binterop.so"' "$temp_dir/publish/MuslPtySmoke.deps.json"
if grep -Fq '"runtimes/linux-x64/native/libhex1binterop.so"' "$temp_dir/publish/MuslPtySmoke.deps.json"; then
    echo "NuGet selected the glibc fallback for linux-musl-x64" >&2
    exit 1
fi
cmp "$library" "$temp_dir/publish/libhex1binterop.so"
bash .github/scripts/assert-musl-native.sh "$temp_dir/publish/libhex1binterop.so"
dotnet "$temp_dir/publish/MuslPtySmoke.dll"
echo "Verified automatic musl build, NuGet RID selection, and packaged PTY interaction on Alpine."
