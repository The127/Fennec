FROM alpine:latest

RUN apk add --no-cache openssh-client bash rsync python3 py3-pip && \
    pip3 install --no-cache-dir websockify --break-system-packages

WORKDIR /launcher
COPY k8s/mac-launcher/launch.sh .
RUN chmod +x launch.sh

ENTRYPOINT ["bash", "launch.sh"]
