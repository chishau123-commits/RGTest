from PIL import Image, ImageDraw
import subprocess
from pathlib import Path
import imageio_ffmpeg

out = Path(r'D:\MyDataInD\Quad\tmp\analysis\game3')
src = r'C:\Users\Chinshyo\Desktop\新建文件夹\Game3.mp4'
ff = imageio_ffmpeg.get_ffmpeg_exe()
times = [i * .25 for i in range(55)]
for i, t in enumerate(times):
    f = out / f'frame_{i:03}_{t:05.2f}.png'
    subprocess.run([ff, '-v', 'error', '-ss', str(t), '-i', src, '-frames:v', '1', str(f), '-y'], check=True)
for page in range(4):
    sheet = Image.new('RGB', (1660, 1064), 'black')
    draw = ImageDraw.Draw(sheet)
    for j in range(16):
        i = page * 16 + j
        if i >= len(times):
            break
        im = Image.open(out / f'frame_{i:03}_{times[i]:05.2f}.png').resize((415, 236))
        x, y = (j % 4) * 415, (j // 4) * 266
        sheet.paste(im, (x, y))
        draw.text((x+5,y+238), f'{times[i]:05.2f}s', fill='white')
    sheet.save(out / f'contact_{page}.png')
