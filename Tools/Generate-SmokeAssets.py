"""Regenerate the small test font and tone. See Docs/WechatSmokeTest.md for prerequisites."""
import json
import math
import struct
import wave
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools import subset

root = Path(__file__).resolve().parents[1]
assets = root / "Assets/_Game/PlatformSmokeTest"
config = json.loads((root / "Configs/SmokeTest.json").read_text(encoding="utf-8"))
chars = "".join(chr(i) for i in range(32, 127)) + config["title"] + config["message"]
font = instantiateVariableFont(TTFont(root / "Logs/NotoSansSC.ttf"), {"wght": 400}, inplace=True)
options = subset.Options()
options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14]
options.name_legacy = True
options.name_languages = [0x409]
subsetter = subset.Subsetter(options=options)
subsetter.populate(text=chars)
subsetter.subset(font)
for record in font["name"].names:
    if record.nameID in (1, 3, 4, 6):
        value = "SmokeSansSC-Regular" if record.nameID == 6 else "Smoke Sans SC"
        record.string = value.encode(record.getEncoding())
(assets / "Fonts").mkdir(parents=True, exist_ok=True)
font.save(assets / "Fonts/SmokeSansSC.ttf")
(assets / "Fonts/OFL.txt").write_bytes((root / "Logs/NotoSansSC-OFL.txt").read_bytes())
(assets / "Audio").mkdir(parents=True, exist_ok=True)
rate = 22050
duration = 0.25
with wave.open(str(assets / "Audio/test-tone.wav"), "wb") as tone:
    tone.setnchannels(1)
    tone.setsampwidth(2)
    tone.setframerate(rate)
    frames = bytearray()
    for i in range(int(rate * duration)):
        envelope = min(1, i / (rate * 0.01)) * max(0, 1 - i / (rate * duration))
        value = int(0.2 * 32767 * envelope * math.sin(2 * math.pi * 660 * i / rate))
        frames.extend(struct.pack("<h", value))
    tone.writeframes(frames)
print("Generated font subset and test tone.")
