"""Remove exterior checkerboard only; keep source RGB and canvas unchanged."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

out = Path(__file__).resolve().parent
root = out.parents[2]
names = ['newspaper_04_pitchfur', 'newspaper_03_rolling_hairballs']
for name in names:
    source = out / (name + '_original.png')
    if not source.exists():
        source = root / 'Assets/Sprites/UI/ResultNewspaper' / (name + '.png')
    original = Image.open(source).convert('RGBA')
    rgb = np.asarray(original)[:, :, :3].copy()
    # Both sheets have a closed, very dark pixel-art outer outline.
    barrier = (rgb.max(axis=2) < 120).astype('uint8') * 255
    flood = Image.fromarray(barrier).copy()
    ImageDraw.floodfill(flood, (0, 0), 128, thresh=0)
    enclosed = Image.fromarray((np.asarray(flood) != 128).astype('uint8') * 255).copy()
    ImageDraw.floodfill(enclosed, (original.width // 2, original.height // 2), 64, thresh=0)
    keep = np.asarray(enclosed) == 64
    assert .65 < keep.mean() < .95, (name, keep.mean())
    assert keep[original.height // 2, original.width // 2]
    assert not keep[0].any() and not keep[-1].any()
    assert not keep[:, 0].any() and not keep[:, -1].any()
    rgba = np.dstack((rgb, keep.astype('uint8') * 255))
    result = Image.fromarray(rgba)
    candidate = out / (name + '_transparent.png')
    result.save(candidate)
    preview = Image.new('RGBA', original.size, '#423654')
    preview.alpha_composite(result)
    preview.convert('RGB').resize((1003, 565)).save(out / (name + '_preview.png'))
    assert np.array_equal(np.asarray(result)[:, :, :3], rgb)
    print(f'{name}: {original.size}, transparent={int((~keep).sum())}, RGB unchanged')
