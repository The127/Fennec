FROM python:3-slim

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    bash \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /tests
COPY smoke-tests/ .
RUN chmod +x *.sh

ENTRYPOINT ["bash", "screen-share-smoke.sh", "--no-restart"]
