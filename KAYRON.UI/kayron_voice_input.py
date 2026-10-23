import asyncio
import os
import sys
import time
import wave
import tempfile
import winreg
from collections import deque

sys.path.insert(0, r'C:\Temp\kayron-live-deps')
import numpy as np
import sounddevice as sd
from google import genai
from google.genai import types

RATE = 16000
DEVICE = 1
BLOCK_SECONDS = 0.1
BLOCK_SIZE = int(RATE * BLOCK_SECONDS)
PRE_ROLL_SECONDS = 0.6
SILENCE_SECONDS = 1.1
START_TIMEOUT_SECONDS = 12
MAX_SPEECH_SECONDS = 30
THRESHOLD_RMS = 260


def capturar_fala():
    pre_roll = deque(maxlen=max(1, int(PRE_ROLL_SECONDS / BLOCK_SECONDS)))
    gravacao = []
    iniciou_fala = False
    instante_inicio = time.monotonic()
    instante_inicio_fala = None
    instante_ultima_fala = instante_inicio

    print("KAYRON_LISTENING=1", flush=True)

    with sd.InputStream(
        samplerate=RATE,
        channels=1,
        dtype="int16",
        device=DEVICE,
        blocksize=BLOCK_SIZE,
    ) as stream:
        while True:
            bloco, overflow = stream.read(BLOCK_SIZE)
            agora = time.monotonic()
            bloco = np.asarray(bloco, dtype=np.int16).copy()
            nivel = float(np.sqrt(np.mean(bloco.astype(np.float32) ** 2)))

            if not iniciou_fala:
                pre_roll.append(bloco)
                if nivel >= THRESHOLD_RMS:
                    iniciou_fala = True
                    instante_inicio_fala = agora
                    gravacao.extend(pre_roll)
                    instante_ultima_fala = agora
                elif agora - instante_inicio >= START_TIMEOUT_SECONDS:
                    print("KAYRON_NO_SPEECH=1", flush=True)
                    return None
                continue

            gravacao.append(bloco)
            if nivel >= THRESHOLD_RMS:
                instante_ultima_fala = agora

            if agora - instante_ultima_fala >= SILENCE_SECONDS:
                break
            if instante_inicio_fala is not None and agora - instante_inicio_fala >= MAX_SPEECH_SECONDS:
                break

    if not gravacao:
        print("KAYRON_NO_SPEECH=1", flush=True)
        return None

    audio = np.concatenate(gravacao, axis=0)
    nivel_audio = float(np.sqrt(np.mean(audio.astype(np.float32) ** 2)))
    if nivel_audio < THRESHOLD_RMS:
        print("KAYRON_NO_SPEECH=1", flush=True)
        return None

    temp = os.path.join(tempfile.gettempdir(), "kayron_voice_input.wav")
    with wave.open(temp, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(RATE)
        wf.writeframes(audio.tobytes())
    return temp


async def main():
    temp = capturar_fala()
    if temp is None:
        return

    with open(temp, "rb") as f:
        data = f.read()

    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as k:
        key = winreg.QueryValueEx(k, "GEMINI_API_KEY")[0]

    client = genai.Client(api_key=key)
    response = await client.aio.models.generate_content(
        model="gemini-3.5-transcribe",
        contents=[types.Content(role="user", parts=[
            types.Part.from_bytes(data=data, mime_type="audio/wav"),
            types.Part.from_text(text="Transcreva literalmente a fala em português brasileiro. Não traduza, não explique e não responda à fala. Retorne apenas o texto reconhecido.")
        ])]
    )

    texts = []
    for candidate in (getattr(response, "candidates", None) or []):
        for part in (getattr(getattr(candidate, "content", None), "parts", None) or []):
            transcription = getattr(part, "audio_transcription", None)
            if transcription and getattr(transcription, "text", None):
                texts.append(transcription.text)
            elif getattr(part, "text", None):
                texts.append(part.text)

    text = " ".join(x.strip() for x in texts if x and x.strip()).strip()
    if not text:
        raise RuntimeError("O serviço não retornou uma transcrição. Tente novamente.")
    print("KAYRON_TRANSCRIPT=" + text, flush=True)


if __name__ == "__main__":
    asyncio.run(main())
