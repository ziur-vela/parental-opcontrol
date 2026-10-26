#!/usr/bin/env python3
# Copyright (c) 2026 Ziur / OpControl IT Solutions
# SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
"""Pin the bundled variable Fredoka/Nunito to the static weights the lock screen uses.

GDI+ (System.Drawing) cannot select an axis on a variable font through a
PrivateFontCollection: it only sees the default instance (Fredoka Light 300,
Nunito ExtraLight 200). The lock screen needs Fredoka 500/600/700 and Nunito
700/800, so each weight is instanced into its own static file with a unique
family name ("Fredoka SemiBold", "Nunito Bold", ...). That makes the family
resolve deterministically in GDI+.

Both source fonts are OFL; the licence files are next to the embedded copies.

    python3 -m venv /tmp/ftenv && /tmp/ftenv/bin/pip install fonttools
    /tmp/ftenv/bin/python tools/fonts/make-static-fonts.py
"""
from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).resolve().parent
TARGET = ROOT / "src" / "OpControl.Parental.Client.Windows.Helper" / "fonts"

JOBS = [
    ("Fredoka-Variable.ttf", "Fredoka-Medium.ttf", 500, "Fredoka", "Medium"),
    ("Fredoka-Variable.ttf", "Fredoka-SemiBold.ttf", 600, "Fredoka", "SemiBold"),
    ("Nunito-Variable.ttf", "Nunito-Bold.ttf", 700, "Nunito", "Bold"),
    ("Nunito-Variable.ttf", "Nunito-ExtraBold.ttf", 800, "Nunito", "ExtraBold"),
]


def build(source: str, target: str, weight: int, family: str, style: str) -> None:
    font = TTFont(SOURCE / source)
    axes = {axis.axisTag: axis.defaultValue for axis in font["fvar"].axes}
    axes["wght"] = weight
    if "wdth" in axes:
        axes["wdth"] = 100
    instance = instantiateVariableFont(font, axes, updateFontNames=False)

    full = f"{family} {style}"
    postscript = f"{family.replace(' ', '')}-{style}"
    name = instance["name"]
    for record in list(name.names):
        if record.nameID in (1, 2, 3, 4, 6, 16, 17):
            name.removeNames(
                nameID=record.nameID,
                platformID=record.platformID,
                platEncID=record.platEncID,
                langID=record.langID,
            )
    name.setName(family, 1, 3, 1, 0x409)
    name.setName("Regular", 2, 3, 1, 0x409)
    name.setName(f"{postscript};Job18.6", 3, 3, 1, 0x409)
    name.setName(full, 4, 3, 1, 0x409)
    name.setName(postscript, 6, 3, 1, 0x409)
    name.setName(family, 16, 3, 1, 0x409)
    name.setName("Regular", 17, 3, 1, 0x409)

    os2 = instance["OS/2"]
    os2.usWeightClass = weight
    # One face per family: keep REGULAR, never the bold/italic bits.
    os2.fsSelection = (os2.fsSelection & ~0b100001) | 0b1000000
    instance["head"].macStyle = 0

    instance.save(TARGET / target)
    print(f"{target}: {full} ({weight})")


if __name__ == "__main__":
    for job in JOBS:
        build(*job)
