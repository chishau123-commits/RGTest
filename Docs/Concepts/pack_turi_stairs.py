"""Mechanical slicing/alignment of the generated 4x2 stair sheet; preserves source artwork."""
import sys, zipfile
from pathlib import Path
from PIL import Image
source=Image.open(sys.argv[1]).convert('RGBA')
root=Path(__file__).resolve().parents[2]
pack=root/'Docs/Concepts/TuriDuskSprites'
atlas=Image.new('RGBA',(192,128))
for row in range(2):
    for col in range(4):
        cell=source.crop((round(col*source.width/4),round(row*source.height/2),
                          round((col+1)*source.width/4),round((row+1)*source.height/2)))
        bbox=cell.getchannel('A').point(lambda x:255 if x>100 else 0).getbbox()
        if not bbox: raise ValueError('Empty stair frame')
        figure=cell.crop(bbox)
        figure=figure.resize((round(54*figure.width/figure.height),54),Image.Resampling.NEAREST)
        frame=Image.new('RGBA',(48,64));frame.paste(figure,((48-figure.width)//2,6))
        frame.save(pack/f'turi-{"down" if row==0 else "up"}-{col+1:02}.png')
        atlas.paste(frame,(col*48,row*64))
atlas.save(root/'Assets/RhythmDemo/Resources/Dusk/turi-stairs.png')
atlas.save(pack/'turi-stairs.png')
(pack/'STAIRS.txt').write_text('Stairs: 4 front/down + 4 back/up frames, 6 fps. Atlas 192x128, cells 48x64.\nTop row down, bottom row up. Feet baseline y=60. Original horizontal animations unchanged.\n')
with zipfile.ZipFile(root/'Docs/Concepts/TuriDuskSprites.zip','w',zipfile.ZIP_DEFLATED) as archive:
    for file in pack.iterdir():
        if file.is_file():archive.write(file,file.name)
