FROM python:3.12-slim

RUN apt-get update && apt-get install -y --no-install-recommends novnc openssh-client && \
    rm -rf /var/lib/apt/lists/*

RUN pip install --no-cache-dir fastapi uvicorn httpx websockets cryptography

WORKDIR /app
COPY server.py .
COPY static/ static/

# Bundle noVNC static files so we can serve vnc.html + proxy WS
RUN cp -r /usr/share/novnc/. static/novnc/

CMD ["uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8080"]
