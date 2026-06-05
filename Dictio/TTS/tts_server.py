"""
Chatterbox Turbo Local API Server
----------------------------------
Lives in:  Dictio/TTS/tts_server.py
Voice ref: Dictio/TTS/voice.wav  (hardcoded, always used)
Venv:      Dictio/TTS/.venv/

Started automatically by the C# ServerManager — do not run manually.
"""

import os
import io
import base64
import traceback

import torch
import soundfile as sf
import uvicorn
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

# ---------------------------------------------------------------------------
# Paths — everything is relative to this script's own directory (TTS/)
# ---------------------------------------------------------------------------
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
VOICE_WAV   = os.path.join(SCRIPT_DIR, "voice.wav")

# ---------------------------------------------------------------------------
# Device
# ---------------------------------------------------------------------------
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

# ---------------------------------------------------------------------------
# FastAPI app
# ---------------------------------------------------------------------------
app = FastAPI(title="Chatterbox Turbo Local Server", version="1.0")

# Model is loaded once on the first /tts request (lazy load keeps startup fast)
_model = None


def get_model():
    global _model
    if _model is None:
        print(f"[Chatterbox] Loading model on {DEVICE.upper()} …", flush=True)
        from chatterbox.tts_turbo import ChatterboxTurboTTS
        _model = ChatterboxTurboTTS.from_pretrained(device=DEVICE)
        print("[Chatterbox] Model ready.", flush=True)
    return _model


# ---------------------------------------------------------------------------
# Schemas
# ---------------------------------------------------------------------------
class TtsRequest(BaseModel):
    text:        str
    exaggeration: float = 0.4   # 0.0–1.0  emotion/expressiveness
    cfg_weight:   float = 0.7   # classifier-free guidance strength
    temperature:  float = 0.8   # sampling temperature
    top_p:        float = 0.95  # nucleus sampling


class TtsResponse(BaseModel):
    audio_b64:   str   # base64-encoded WAV
    sample_rate: int
    device:      str


# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------
@app.get("/health")
def health():
    return {
        "status":           "ok",
        "device":           DEVICE,
        "cuda_device_name": torch.cuda.get_device_name(0) if DEVICE == "cuda" else "N/A",
        "model_loaded":     _model is not None,
        "voice_wav":        VOICE_WAV,
        "voice_wav_exists": os.path.isfile(VOICE_WAV),
    }


@app.post("/tts", response_model=TtsResponse)
def generate_speech(req: TtsRequest):
    if not req.text.strip():
        raise HTTPException(status_code=400, detail="text must not be empty")

    if not os.path.isfile(VOICE_WAV):
        raise HTTPException(
            status_code=500,
            detail=f"voice.wav not found at: {VOICE_WAV}"
        )

    try:
        model = get_model()

        wav = model.generate(
            req.text,
            audio_prompt_path=VOICE_WAV,
            exaggeration=req.exaggeration,
            cfg_weight=req.cfg_weight,
            temperature=req.temperature,
            top_p=req.top_p,
        )

        # tensor → numpy → WAV bytes in memory
        wav_np = wav.squeeze().cpu().numpy()
        buf = io.BytesIO()
        sf.write(buf, wav_np, model.sr, format="WAV")
        buf.seek(0)

        return TtsResponse(
            audio_b64=base64.b64encode(buf.read()).decode(),
            sample_rate=model.sr,
            device=DEVICE,
        )

    except Exception as exc:
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(exc))


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    print(f"[Chatterbox] Device  : {DEVICE.upper()}", flush=True)
    print(f"[Chatterbox] Voice   : {VOICE_WAV}", flush=True)
    print(f"[Chatterbox] Voice OK: {os.path.isfile(VOICE_WAV)}", flush=True)
    uvicorn.run(app, host="127.0.0.1", port=7860, log_level="warning")