# Smoke Tests

Runs the API, a Linux app instance, and a Mac Mini app instance on k3s, then executes the smoke test suite.

## Prerequisites

- k3s node reachable via SSH
- Mac Mini on the same LAN, reachable via SSH from the k3s node
- `docker`, `kubectl`, `rsync`, `envsubst` installed locally
- [just](https://github.com/casey/just) command runner
- TLS cert for your domain (see ingress.yaml — uses Traefik TLS passthrough)

## Setup

### 1. Configure environment

```bash
cp k8s/smoke.env.example k8s/smoke.env
# Edit k8s/smoke.env with your values (IPs, domain, SSH user, paths)
```

### 2. Create k8s secrets

```bash
kubectl create namespace fennec-test

# RSA key for JWT signing
kubectl create secret generic fennec-keys \
  --from-file=private.pem=./private.pem -n fennec-test

# SSH key for Mac Mini access
kubectl create secret generic mac-mini-ssh \
  --from-file=id_ed25519=~/.ssh/id_ed25519 -n fennec-test

# Test user credentials + seed data IDs
kubectl create secret generic fennec-test-env \
  --from-literal=FENNEC_AUTO_LOGIN_LOCAL='user@https://YOUR_DOMAIN' \
  --from-literal=FENNEC_AUTO_LOGIN_PASSWORD_LOCAL='password' \
  --from-literal=FENNEC_AUTO_LOGIN_MINI='mini@https://YOUR_DOMAIN' \
  --from-literal=FENNEC_AUTO_LOGIN_PASSWORD_MINI='password' \
  --from-literal=FENNEC_AUTO_JOIN_SERVER='<server-uuid-from-seed>' \
  --from-literal=FENNEC_AUTO_JOIN_CHANNEL='<channel-uuid-from-seed>' \
  -n fennec-test
```

### 3. TLS certificate

Create a `fennec-api-tls` secret with your domain's cert as a PFX, and a `fennec-api-ca` configmap if using a custom CA:

```bash
kubectl create secret generic fennec-api-tls \
  --from-file=tls.pfx=./cert.pfx -n fennec-test

kubectl create configmap fennec-api-ca \
  --from-file=ca.crt=./ca.crt -n fennec-test
```

## Run

```bash
just smoke-test    # builds images, deploys to k3s, runs tests
just smoke-teardown  # delete the fennec-test namespace
```

`smoke-build` and `smoke-test` read `k8s/smoke.env` and use `envsubst` to template the k8s manifests before applying.
