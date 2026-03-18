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

# build smoke test container images and import into k3s
smoke-build:
    #!/usr/bin/env bash
    set -euo pipefail
    source k8s/smoke.env
    docker build -t fennec-api -f Fennec.Api/Dockerfile .
    docker build -t fennec-app-linux -f Fennec.App.Desktop/Dockerfile.smoke .
    docker build -t fennec-test-runner -f smoke-tests/Dockerfile .
    docker build -t fennec-mac-launcher -f k8s/mac-launcher/Dockerfile .
    docker build -t fennec-smoke-dashboard -f k8s/smoke-dashboard/Dockerfile k8s/smoke-dashboard/
    echo "Importing images into k3s on ${SMOKE_K3S_HOST}..."
    docker save fennec-api fennec-app-linux fennec-test-runner fennec-mac-launcher fennec-smoke-dashboard | ssh "$SMOKE_K3S_HOST" 'sudo k3s ctr images import -'

# build and deploy smoke dashboard only (faster than full smoke-build)
smoke-dashboard: _smoke-dashboard-build
    kubectl apply -f k8s/smoke-dashboard.yaml
    kubectl rollout restart deployment/smoke-dashboard -n fennec-test
    kubectl rollout status deployment/smoke-dashboard -n fennec-test --timeout=60s

_smoke-dashboard-build:
    #!/usr/bin/env bash
    set -euo pipefail
    source k8s/smoke.env
    docker build -t fennec-smoke-dashboard -f k8s/smoke-dashboard/Dockerfile k8s/smoke-dashboard/
    docker save fennec-smoke-dashboard | ssh "$SMOKE_K3S_HOST" 'sudo k3s ctr images import -'

# deploy and run smoke tests on k3s
smoke-test: smoke-build
    #!/usr/bin/env bash
    set -euo pipefail
    source k8s/smoke.env
    export SMOKE_NODE_IP SMOKE_DOMAIN SMOKE_MINI_IP SMOKE_MINI_SSH_USER SMOKE_SOURCE_PATH
    NS=fennec-test

    # Sync source to k3s node for mac-launcher hostPath mount
    rsync -az --delete --exclude .git/ --exclude bin/ --exclude obj/ . "${SMOKE_K3S_HOST}:${SMOKE_SOURCE_PATH}/"

    # Apply static manifests
    kubectl apply -f k8s/namespace.yaml
    kubectl apply -f k8s/postgres-seed-configmap.yaml
    kubectl apply -f k8s/postgres.yaml

    # Apply templated manifests via envsubst
    envsubst < k8s/fennec-api.yaml | kubectl apply -f -
    envsubst < k8s/fennec-app-local.yaml | kubectl apply -f -
    kubectl delete job mac-launcher -n $NS --ignore-not-found
    envsubst < k8s/fennec-app-mini.yaml | kubectl apply -f -
    envsubst < k8s/ingress.yaml | kubectl apply -f -
    kubectl apply -f k8s/smoke-dashboard.yaml

    # Wait for core services
    kubectl wait --for=condition=available deployment/postgres -n $NS --timeout=60s
    kubectl wait --for=condition=available deployment/fennec-api -n $NS --timeout=120s

    # Run seed job (idempotent — skips if data exists)
    kubectl delete job seed-db -n $NS --ignore-not-found
    kubectl apply -f k8s/seed-job.yaml
    kubectl wait --for=condition=complete job/seed-db -n $NS --timeout=120s

    # Wait for app instances
    kubectl wait --for=condition=available deployment/fennec-app-local -n $NS --timeout=120s

    # Run smoke tests
    kubectl delete job smoke-test-template -n $NS --ignore-not-found
    kubectl apply -f k8s/test-runner.yaml
    kubectl wait --for=condition=complete job/smoke-test-template -n $NS --timeout=600s || true
    kubectl logs job/smoke-test-template -n $NS

# tear down the smoke test k3s environment
smoke-teardown:
    kubectl delete namespace fennec-test --ignore-not-found

# build native video library for Windows (x64)
build-native-windows:
    cmake -S native -B native/build-windows -DCMAKE_BUILD_TYPE=Release
    cmake --build native/build-windows --config Release
    mkdir -p Fennec.App/runtimes/win-x64/native/
    cp native/build-windows/windows/Release/fennec_video.dll Fennec.App/runtimes/win-x64/native/

