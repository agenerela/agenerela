# ComfyUI for Agenerela asset generation. docker-compose.yml builds it; see README.md.
#
# Everything ComfyUI needs lives in this image: its own Python, PyTorch and
# ComfyUI checkout. Nothing is shared with another ComfyUI install on the host.
FROM python:3.13-slim-bookworm

# The RTX 50 series (Blackwell) needs a PyTorch built for CUDA 12.8 or later.
# ComfyUI recommends cu130, which needs an NVIDIA driver whose nvidia-smi reports
# "CUDA Version: 13.0" or newer. To upgrade, change these together with the image
# tag in docker-compose.yml, then run `python comfy.py validate`.
ARG COMFYUI_VERSION=v0.39.1
ARG TORCH_VERSION=2.14.1
ARG TORCHVISION_VERSION=0.29.1
ARG TORCH_INDEX_URL=https://download.pytorch.org/whl/cu130

ENV PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    NVIDIA_VISIBLE_DEVICES=all \
    NVIDIA_DRIVER_CAPABILITIES=compute,utility

# gcc and libc6-dev are for Triton, which compiles a small C helper the first time
# it runs. PyTorch 2.14 routes some ordinary matrix products through Triton
# kernels, so without a compiler the sfx workflow's text encoder fails.
RUN apt-get update \
 && apt-get install -y --no-install-recommends git libgomp1 gcc libc6-dev \
 && rm -rf /var/lib/apt/lists/*

RUN pip install torch==${TORCH_VERSION} torchvision==${TORCHVISION_VERSION} --index-url ${TORCH_INDEX_URL}

# The constraints file keeps ComfyUI's requirements from swapping PyTorch for a
# different build: a conflict fails the build instead of silently replacing it.
RUN git clone --depth 1 --branch ${COMFYUI_VERSION} https://github.com/Comfy-Org/ComfyUI.git /opt/ComfyUI \
 && printf 'torch==%s\ntorchvision==%s\n' "${TORCH_VERSION}" "${TORCHVISION_VERSION}" > /tmp/constraints.txt \
 && pip install -r /opt/ComfyUI/requirements.txt -c /tmp/constraints.txt \
 && rm /tmp/constraints.txt

# Run as an ordinary user. /data/models must exist and belong to that user here,
# because an empty Docker volume copies the ownership of the folder it mounts on.
# /data/custom_nodes must exist too: with --base-directory=/data, ComfyUI lists it
# at startup and exits if it is missing. It stays empty (README, "Adding a workflow").
RUN useradd --create-home --uid 1000 comfy \
 && mkdir -p /data/models /data/input /data/output /data/user /data/custom_nodes /opt/agenerela \
 && chown -R comfy:comfy /data /opt/ComfyUI

USER comfy
WORKDIR /opt/ComfyUI
EXPOSE 8188
CMD ["python", "main.py", "--listen", "0.0.0.0", "--port", "8188", "--base-directory", "/data"]
