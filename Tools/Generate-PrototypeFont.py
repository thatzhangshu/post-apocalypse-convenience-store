"""Generate a separate Chinese font subset from prototype config and runtime UI text."""
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools import subset

root = Path(__file__).resolve().parents[1]
source = root / "Logs/NotoSansSC.ttf"
license_path = root / "Logs/NotoSansSC-OFL.txt"
if not source.exists() or not license_path.exists():
    raise SystemExit("Place official Noto Sans SC source and OFL in Logs; see Docs/WechatSmokeTest.md.")
chars = "".join(chr(i) for i in range(32, 127))
chars += (root / "Configs/Prototype.json").read_text(encoding="utf-8")
for script in (root / "Assets/_Game/Prototype/Runtime").glob("*.cs"):
    chars += script.read_text(encoding="utf-8")
font = instantiateVariableFont(TTFont(source), {"wght": 400}, inplace=True)
options = subset.Options()
options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14]
options.name_legacy = True
options.name_languages = [0x409]
subsetter = subset.Subsetter(options=options)
subsetter.populate(text=chars)
subsetter.subset(font)
for record in font["name"].names:
    if record.nameID in (1, 3, 4, 6):
        value = "PrototypeSansSC-Regular" if record.nameID == 6 else "Prototype Sans SC"
        record.string = value.encode(record.getEncoding())
output = root / "Assets/_Game/Prototype/Fonts"
output.mkdir(parents=True, exist_ok=True)
font.save(output / "PrototypeSansSC.ttf")
(output / "OFL.txt").write_bytes(license_path.read_bytes())
print(f"Generated prototype font: {len(set(chars))} source characters, {(output / 'PrototypeSansSC.ttf').stat().st_size} bytes.")
