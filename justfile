# generate a local private key for development purposes
generate-private-key:
    openssl genrsa -out private.pem 2048

# build native video library for macOS (arm64)
build-native-macos:
    cmake -S native -B native/build-macos -DCMAKE_BUILD_TYPE=Release
    cmake --build native/build-macos
    mkdir -p Fennec.App/runtimes/osx-arm64/native/
    cp native/build-macos/macos/libfennec_video.dylib Fennec.App/runtimes/osx-arm64/native/

# build native video library for Linux (x64)
build-native-linux:
    cmake -S native -B native/build-linux -DCMAKE_BUILD_TYPE=Release
    cmake --build native/build-linux
    mkdir -p Fennec.App/runtimes/linux-x64/native/
    find native/build-linux/linux -name 'lib*.so*' -not -type l -exec cp {} Fennec.App/runtimes/linux-x64/native/ \;
    cd Fennec.App/runtimes/linux-x64/native/ && \
      for f in lib*.so.*.*.*; do \
        soname=$(objdump -p "$f" 2>/dev/null | awk '/SONAME/{print $2}'); \
        [ -n "$soname" ] && ln -sf "$f" "$soname"; \
      done

# create a release tag; bumps patch by default — pass bump=minor or bump=major to bump those instead
release bump="patch":
    #!/usr/bin/env bash
    set -euo pipefail
    latest=$(git tag --sort=-v:refname | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | head -1)
    version="${latest:-v0.0.0}"
    version="${version#v}"
    major=$(echo "$version" | cut -d. -f1)
    minor=$(echo "$version" | cut -d. -f2)
    patch=$(echo "$version" | cut -d. -f3)
    case "{{bump}}" in
        major) major=$((major + 1)); minor=0; patch=0 ;;
        minor) minor=$((minor + 1)); patch=0 ;;
        patch) patch=$((patch + 1)) ;;
        *) echo "Unknown bump type: {{bump}} (use patch, minor, or major)"; exit 1 ;;
    esac
    new_tag="v${major}.${minor}.${patch}"
    echo "Tagging $new_tag"
    git tag "$new_tag"
    git push origin "$new_tag"

# build native video library for Windows (x64)
build-native-windows:
    cmake -S native -B native/build-windows -DCMAKE_BUILD_TYPE=Release
    cmake --build native/build-windows --config Release
    mkdir -p Fennec.App/runtimes/win-x64/native/
    cp native/build-windows/windows/Release/fennec_video.dll Fennec.App/runtimes/win-x64/native/
