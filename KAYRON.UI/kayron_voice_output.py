import asyncio
import re
import sys
import tempfile
from pathlib import Path

TTS_PACKAGES = r"C:\Temp\KAYRON_Multivozes_PoV_20261006\multivozes_br_engine\venv\Lib\site-packages"
sys.path.insert(0, TTS_PACKAGES)
import edge_tts

VOICE = "pt-BR-AntonioNeural"
MAX_AUDIO_CHARS = 300


def preparar_texto_falado(texto):
    texto = re.sub(r"\s+", " ", texto).strip()
    if len(texto) <= MAX_AUDIO_CHARS:
        return texto

    trecho = texto[:MAX_AUDIO_CHARS + 1]
    finais = [trecho.rfind(sinal) for sinal in (". ", "? ", "! ")]
    fim = max(finais)
    if fim >= 120:
        return trecho[:fim + 1].strip()

    return trecho.rsplit(" ", 1)[0].rstrip(" ,;:") + "..."


async def main():
    texto = sys.stdin.read().strip()
    if not texto:
        raise RuntimeError("A resposta atual do KAYRON está vazia.")

    texto = preparar_texto_falado(texto)
    output = Path(tempfile.gettempdir()) / "kayron_voice_response.mp3"
    await edge_tts.Communicate(texto, VOICE, rate="-5%").save(str(output))

    if not output.exists() or output.stat().st_size < 1000:
        raise RuntimeError("O sintetizador não gerou um áudio válido.")

    print("KAYRON_TTS_FILE=" + str(output), flush=True)


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except Exception as exc:
        print(f"KAYRON_VOICE_OUTPUT_ERROR: {type(exc).__name__}: {exc}", file=sys.stderr, flush=True)
        raise
