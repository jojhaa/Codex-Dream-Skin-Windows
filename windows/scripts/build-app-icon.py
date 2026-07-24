"""Build the Windows icon and package logo assets from one square PNG master."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ICON_SIZES = (16, 24, 32, 48, 64, 128, 256)


def prepare_master(source: Path) -> Image.Image:
    image = Image.open(source).convert("RGBA")
    alpha = image.getchannel("A")
    if alpha.getextrema()[0] < 255:
        bounds = alpha.getbbox()
        if bounds is None:
            raise ValueError("The icon source is fully transparent.")
        symbol = image.crop(bounds)
        symbol.thumbnail((860, 860), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
        canvas.alpha_composite(
            symbol,
            ((1024 - symbol.width) // 2, (1024 - symbol.height) // 2),
        )
        return canvas

    side = min(image.size)
    left = (image.width - side) // 2
    top = (image.height - side) // 2
    image = image.crop((left, top, left + side, top + side))
    image = image.resize((1024, 1024), Image.Resampling.LANCZOS)

    mask = Image.new("L", image.size, 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle((0, 0, 1023, 1023), radius=150, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(0.65))
    image.putalpha(mask)
    return image


def resize_icon(master: Image.Image, size: tuple[int, int], padding: float = 0) -> Image.Image:
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    available = (
        max(1, round(size[0] * (1 - padding * 2))),
        max(1, round(size[1] * (1 - padding * 2))),
    )
    icon = master.copy()
    icon.thumbnail(available, Image.Resampling.LANCZOS)
    x = (size[0] - icon.width) // 2
    y = (size[1] - icon.height) // 2
    canvas.alpha_composite(icon, (x, y))
    return canvas


def save_png(master: Image.Image, path: Path, size: tuple[int, int], padding: float = 0) -> None:
    resize_icon(master, size, padding).save(path, optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("assets", type=Path)
    args = parser.parse_args()

    assets = args.assets.resolve()
    assets.mkdir(parents=True, exist_ok=True)
    master = prepare_master(args.source.resolve())
    master.save(assets / "AppIconMaster.png", optimize=True)
    master.save(
        assets / "AppIcon.ico",
        format="ICO",
        sizes=[(size, size) for size in ICON_SIZES],
        bitmap_format="png",
    )

    save_png(master, assets / "Square150x150Logo.scale-200.png", (300, 300), 0.04)
    save_png(master, assets / "Square44x44Logo.scale-200.png", (88, 88), 0.08)
    save_png(master, assets / "Square44x44Logo.targetsize-24_altform-unplated.png", (24, 24), 0.08)
    save_png(master, assets / "Square44x44Logo.targetsize-48_altform-lightunplated.png", (48, 48), 0.08)
    save_png(master, assets / "StoreLogo.png", (50, 50), 0.08)
    save_png(master, assets / "LockScreenLogo.scale-200.png", (48, 48), 0.08)
    save_png(master, assets / "Wide310x150Logo.scale-200.png", (620, 300), 0.08)
    save_png(master, assets / "SplashScreen.scale-200.png", (1240, 600), 0.20)


if __name__ == "__main__":
    main()
