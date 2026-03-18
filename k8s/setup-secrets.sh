#!/usr/bin/env bash
# Idempotently create all secrets and configmaps needed by the smoke test environment.
# Expects SMOKE_DOMAIN to be set in the environment (sourced from smoke.env by the justfile).
set -euo pipefail

NS=fennec-test

kubectl apply -f k8s/namespace.yaml

apply() {
    # Pipe dry-run yaml through kubectl apply so create is idempotent
    "$@" --dry-run=client -o yaml | kubectl apply -f -
}

# fennec-keys: RSA private key for JWT signing
apply kubectl create secret generic fennec-keys \
    --from-file=private.pem=./private.pem \
    -n "$NS"

# mac-mini-ssh: SSH key for mac-launcher to reach the Mac Mini
apply kubectl create secret generic mac-mini-ssh \
    --from-file=id_ed25519="$HOME/.ssh/id_ed25519" \
    -n "$NS"

# fennec-test-env: runtime config injected into app pods
apply kubectl create secret generic fennec-test-env \
    --from-literal=FENNEC_AUTO_LOGIN_LOCAL="kris@https://${SMOKE_DOMAIN}" \
    --from-literal=FENNEC_AUTO_LOGIN_PASSWORD_LOCAL="kris" \
    --from-literal=FENNEC_AUTO_LOGIN_MINI="mini@https://${SMOKE_DOMAIN}" \
    --from-literal=FENNEC_AUTO_LOGIN_PASSWORD_MINI="mini" \
    --from-literal=FENNEC_AUTO_JOIN_SERVER="046e1f2a-2faf-4686-ba85-fd2448e57a87" \
    --from-literal=FENNEC_AUTO_JOIN_CHANNEL="6a2989c8-be88-42c3-9a69-a84c02586259" \
    --from-literal=MINI_VNC_PASSWORD="Marsters5%2" \
    --from-literal=MINI_VNC_USERNAME="" \
    -n "$NS"

# fennec-api-tls: TLS cert for the API, extracted from Traefik acme.json on p-matrix
# Runs remotely via SSH, converts to PKCS#12 (no password), cleans up temp files.
ssh p-matrix 'bash -s' << 'REMOTE_EOF'
set -euo pipefail
sudo python3 - << 'PYEOF'
import json, pathlib, base64

acme = json.loads(pathlib.Path('/srv/matrix.nymann.dev/traefik/certs/acme.json').read_text())

# Walk all resolvers to find the fennec cert
cert_entry = None
for resolver in acme.values():
    for entry in resolver.get('Certificates', []) or []:
        domains = entry.get('domain', {})
        main = domains.get('main', '')
        if 'fennec' in main:
            cert_entry = entry
            break
    if cert_entry:
        break

if not cert_entry:
    raise SystemExit('fennec cert not found in acme.json')

pathlib.Path('/tmp/_fennec.crt').write_bytes(base64.b64decode(cert_entry['certificate']))
pathlib.Path('/tmp/_fennec.key').write_bytes(base64.b64decode(cert_entry['key']))
print('cert and key written to /tmp/_fennec.{crt,key}')
PYEOF

openssl pkcs12 -export \
    -in /tmp/_fennec.crt \
    -inkey /tmp/_fennec.key \
    -out /tmp/_fennec.pfx \
    -passout pass:
echo 'pfx written to /tmp/_fennec.pfx'
REMOTE_EOF

# Copy pfx locally, create secret, clean up
tmpdir=$(mktemp -d)
trap 'rm -rf "$tmpdir"' EXIT

scp p-matrix:/tmp/_fennec.pfx "$tmpdir/tls.pfx"
ssh p-matrix 'sudo rm -f /tmp/_fennec.crt /tmp/_fennec.key /tmp/_fennec.pfx'

apply kubectl create secret generic fennec-api-tls \
    --from-file=tls.pfx="$tmpdir/tls.pfx" \
    -n "$NS"

# fennec-api-ca: LE cert is publicly trusted; init container handles missing certs gracefully
apply kubectl create configmap fennec-api-ca \
    --from-literal=placeholder="" \
    -n "$NS"

echo "All secrets and configmaps applied to namespace $NS."
