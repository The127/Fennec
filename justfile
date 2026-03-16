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
    echo "Tagging ${latest:-v0.0.0} -> $new_tag"
    git tag "$new_tag"
    git push origin "$new_tag"

# build native video library for Windows (x64)
build-native-windows:
    cmake -S native -B native/build-windows -DCMAKE_BUILD_TYPE=Release
    cmake --build native/build-windows --config Release
    mkdir -p Fennec.App/runtimes/win-x64/native/
    cp native/build-windows/windows/Release/fennec_video.dll Fennec.App/runtimes/win-x64/native/

# --- Screen share test harness ---

# resolve alias from test-hosts.conf to user@host
[private]
resolve-host alias:
    #!/usr/bin/env bash
    set -euo pipefail
    conf="test-hosts.conf"
    if [ ! -f "$conf" ]; then echo "Missing $conf"; exit 1; fi
    host=$(grep -E "^{{alias}}=" "$conf" | head -1 | cut -d= -f2-)
    if [ -z "$host" ]; then echo "Unknown host alias: {{alias}}"; exit 1; fi
    echo "$host"

# rsync source to remote ~/fennec/
test-sync host:
    #!/usr/bin/env bash
    set -euo pipefail
    remote=$(just resolve-host {{host}})
    rsync -az --delete \
      --exclude '.git/' --exclude 'bin/' --exclude 'obj/' \
      --exclude 'test-logs/' --exclude 'test-hosts.conf' \
      ./ "$remote:~/fennec/"

# build on remote host
test-build host:
    #!/usr/bin/env bash
    set -euo pipefail
    remote=$(just resolve-host {{host}})
    ssh "$remote" 'export PATH="$HOME/.dotnet:$PATH" && cd ~/fennec && dotnet build Fennec.App.Desktop/Fennec.App.Desktop.csproj'

# sync + build on remote host
test-deploy host: (test-sync host) (test-build host)

# deploy and launch app on remote host with auto-login and auto-join
test-app host login="" password="" server_id="" channel_id="":
    #!/usr/bin/env bash
    set -euo pipefail
    remote=$(just resolve-host {{host}})
    env_vars=""
    if [ -n "{{login}}" ]; then
      env_vars="FENNEC_AUTO_LOGIN={{login}} FENNEC_AUTO_LOGIN_PASSWORD={{password}}"
    fi
    if [ -n "{{server_id}}" ]; then
      env_vars="$env_vars FENNEC_AUTO_JOIN_SERVER={{server_id}} FENNEC_AUTO_JOIN_CHANNEL={{channel_id}}"
    fi
    # detect remote OS for display setup
    remote_os=$(ssh "$remote" 'uname')
    launch_prefix=""
    if [ "$remote_os" = "Linux" ]; then
      launch_prefix="DISPLAY=:0"
    elif [ "$remote_os" = "Darwin" ]; then
      launch_prefix="caffeinate -d"
    fi
    just test-deploy {{host}}
    mkdir -p test-logs
    ssh "$remote" "$env_vars $launch_prefix bash -c 'export PATH=\"\$HOME/.dotnet:\$PATH\" && cd ~/fennec && dotnet run --project Fennec.App.Desktop 2>&1 | tee ~/fennec/test.log'" 2>&1 | tee "test-logs/{{host}}.log"

# run app locally with optional profile, auto-login, and auto-join
test-app-local profile="" login="" password="" server_id="" channel_id="":
    #!/usr/bin/env bash
    set -euo pipefail
    if [ -n "{{profile}}" ]; then export FENNEC_PROFILE="{{profile}}"; fi
    if [ -n "{{login}}" ]; then
      export FENNEC_AUTO_LOGIN="{{login}}"
      export FENNEC_AUTO_LOGIN_PASSWORD="{{password}}"
    fi
    if [ -n "{{server_id}}" ]; then
      export FENNEC_AUTO_JOIN_SERVER="{{server_id}}"
      export FENNEC_AUTO_JOIN_CHANNEL="{{channel_id}}"
    fi
    mkdir -p test-logs
    profile_name="${FENNEC_PROFILE:-default}"
    dotnet run --project Fennec.App.Desktop 2>&1 | tee "test-logs/local-${profile_name}.log"

# tail remote test log
test-logs host:
    #!/usr/bin/env bash
    set -euo pipefail
    remote=$(just resolve-host {{host}})
    ssh "$remote" 'tail -f ~/fennec/test.log'

# fetch remote log to local test-logs/<host>-<timestamp>.log
test-logs-fetch host:
    #!/usr/bin/env bash
    set -euo pipefail
    remote=$(just resolve-host {{host}})
    mkdir -p test-logs
    ts=$(date +%Y%m%d-%H%M%S)
    scp "$remote:~/fennec/test.log" "test-logs/{{host}}-${ts}.log"
    echo "Saved to test-logs/{{host}}-${ts}.log"

# run the API server
test-api:
    mkdir -p test-logs
    FennecSettings__IssuerUrl=https://fennec.matrix.nymann.dev ASPNETCORE_URLS=https://0.0.0.0:7014 dotnet run --project Fennec.Api 2>&1 | tee test-logs/api.log
