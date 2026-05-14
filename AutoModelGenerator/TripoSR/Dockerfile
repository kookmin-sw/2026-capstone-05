# 다시 CUDA 컴파일러(nvcc)가 포함된 안전한 devel 이미지로 복귀합니다.
FROM nvidia/cuda:11.8.0-cudnn8-devel-ubuntu22.04

# 시스템 의존성 설치 (C++ 빌드 도구 필수 포함)
RUN apt-get update && apt-get install -y \
    git \
    python3-pip \
    python3-dev \
    libgl1 \
    libglib2.0-0 \
    ninja-build \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# 파이썬 명령어 연결
RUN ln -s /usr/bin/python3 /usr/bin/python

WORKDIR /app

# 요구사항 파일만 복사
COPY requirements.txt /app/

# 1. 빌드 도구 업데이트
RUN pip install --no-cache-dir --upgrade pip setuptools wheel ninja

# 2. torchmcubes 빌드를 위해 PyTorch(CUDA 11.8) 선행 설치
RUN pip install --no-cache-dir torch torchvision --index-url https://download.pytorch.org/whl/cu118

# 3. requirements.txt 설치 (에러 유발 onnxruntime 제거)
RUN sed -i '/onnxruntime/d' requirements.txt && \
    pip install --no-cache-dir -r requirements.txt

# 4. [UI 및 의존성 충돌 완벽 방어]
# - CUDA 11.8 호환 onnxruntime-gpu
# - Numpy 2.0 충돌 방지
# - Gradio JSON schema bool 에러 방지를 위한 FastAPI/Pydantic 안정화 버전 고정
RUN pip install --no-cache-dir \
    onnxruntime-gpu==1.17.1 \
    "numpy<2.0.0" \
    "gradio==4.36.1" \
    "fastapi<0.113.0" \
    "pydantic<2.8.0" \
    "huggingface_hub<0.26.0"

# 나머지 소스코드 복사
COPY . /app

ENV GRADIO_SERVER_NAME="0.0.0.0"
EXPOSE 7860

CMD ["python", "gradio_app.py"]