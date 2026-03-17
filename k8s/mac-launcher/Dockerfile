FROM alpine:latest

RUN apk add --no-cache openssh-client bash rsync

WORKDIR /launcher
COPY k8s/mac-launcher/launch.sh .
RUN chmod +x launch.sh

ENTRYPOINT ["bash", "launch.sh"]
